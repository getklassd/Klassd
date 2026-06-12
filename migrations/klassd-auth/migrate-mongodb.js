// Klassd.Auth migration — MongoDB (run with mongosh)
// ============================================================================
// Migrates a legacy Klassd CMS users collection (legacy UserRecord shape:
//   _id, Username, Email, PasswordHash, Provider, ExternalId, Disabled, Roles)
// into the Klassd.Auth shape:
//   users:         { _id, Username, PrimaryEmail, Disabled, CreatedAt,
//                    LoginMethods: [ { Id, UserId, Kind, Email, EmailVerified,
//                                      PasswordHash, ProviderId, ProviderUserId, CreatedAt } ] }
//   user_metadata: { _id: <userId>, Json: '{"roles":[...]}' }
//
// Klassd.Auth stores enums as STRINGS ("EmailPassword" / "ThirdParty") and
// DateTimeOffset as an ISO string — this script matches that.
//
// Passwords are preserved: legacy "salt:hash" (PBKDF2-HMAC-SHA256, 100k iters,
// 32-byte key) -> "pbkdf2$100000$salt$hash".
//
// RUN ONCE, BEFORE starting the upgraded app. BACK UP FIRST (mongodump).
//   mongosh "<connection string>" migrate-mongodb.js
//
// VERIFY the three names below against your deployment before running:
//   - DB_NAME:        the database the CMS/auth adapter uses
//   - LEGACY_USERS:   the existing CMS users collection name ("users" or "Users")
//   - field casing:   the script reads PascalCase with a camelCase fallback
// ============================================================================

const DB_NAME      = 'klassd';     // <-- set to your CMS/auth database
const LEGACY_USERS = 'users';      // <-- set to the existing users collection name

const database = db.getSiblingDB(DB_NAME);

// Guard against double-runs.
if (database.getCollectionNames().includes('login_methods') ||
    database.getCollectionNames().includes('users_legacy')) {
    throw new Error('Looks already migrated (login_methods/users_legacy present) — aborting.');
}
const legacy = database.getCollection(LEGACY_USERS);
const sample = legacy.findOne({});
if (sample && sample.LoginMethods !== undefined) {
    throw new Error(`${LEGACY_USERS} already has the Klassd.Auth shape — aborting.`);
}

// Read either PascalCase (legacy class-map default) or camelCase.
const pick = (d, ...names) => { for (const n of names) if (d[n] !== undefined && d[n] !== null) return d[n]; return undefined; };
const nowIso = new Date().toISOString().replace('Z', '+00:00');   // ISO 8601 with offset
const newId  = () => (typeof UUID === 'function' ? UUID().toString().replace(/-/g, '') : Math.random().toString(16).slice(2).padEnd(32, '0'));

// 1) Move the legacy collection aside.
legacy.renameCollection('users_legacy', /*dropTarget*/ false);
const src = database.getCollection('users_legacy');

const users = [];
const metadata = [];

src.find({}).forEach(d => {
    const id        = d._id;
    const email     = pick(d, 'Email', 'email') ?? null;
    const username  = pick(d, 'Username', 'username') ?? null;
    const disabled  = pick(d, 'Disabled', 'disabled') ?? false;
    const pwdHash   = pick(d, 'PasswordHash', 'passwordHash');
    const provider  = pick(d, 'Provider', 'provider');
    const extId     = pick(d, 'ExternalId', 'externalId');
    const roles     = pick(d, 'Roles', 'roles');

    const loginMethods = [];
    if (pwdHash && pwdHash !== '') {
        loginMethods.push({
            Id: newId(), UserId: id, Kind: 'EmailPassword', Email: email, EmailVerified: false,
            PasswordHash: 'pbkdf2$100000$' + String(pwdHash).replace(':', '$'),
            ProviderId: null, ProviderUserId: null, CreatedAt: nowIso,
        });
    }
    if (provider && provider !== 'local' && extId && extId !== '') {
        loginMethods.push({
            Id: newId(), UserId: id, Kind: 'ThirdParty', Email: email, EmailVerified: true,
            PasswordHash: null, ProviderId: provider, ProviderUserId: extId, CreatedAt: nowIso,
        });
    }

    users.push({ _id: id, Username: username, PrimaryEmail: email, Disabled: disabled, CreatedAt: nowIso, LoginMethods: loginMethods });

    // Skip empty roles: no roles == Administrator, by convention.
    if (Array.isArray(roles) && roles.length > 0) {
        metadata.push({ _id: id, Json: JSON.stringify({ roles }) });
    }
});

if (users.length)    database.getCollection('users').insertMany(users);
if (metadata.length) database.getCollection('user_metadata').insertMany(metadata);

print(`Migrated ${users.length} users (${metadata.length} with roles). Legacy kept as 'users_legacy'.`);
print(`Verify login, then: db.getSiblingDB('${DB_NAME}').users_legacy.drop()`);
