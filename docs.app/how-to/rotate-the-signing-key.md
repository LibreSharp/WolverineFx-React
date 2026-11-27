# Change the JWT signing key

`Jwt:SigningKey` signs the access tokens. Change it if it may have leaked, or on a schedule.

1. Generate a new key: `openssl rand -base64 48`.
2. Set it where the app reads it (`Jwt__SigningKey`, or `JWT_SIGNING_KEY` in `deploy/.env`), and
   restart every instance.

Access tokens signed with the old key stop working at once. Refresh tokens don't depend on the
key (they're random values, stored hashed), so signed-in users' browsers get a 401, refresh, and
carry on without noticing.

## If the key leaked

Someone with the key could have made their own access tokens. Changing the key stops those at
once. To also sign everyone out, delete the stored refresh tokens:

<!--#if (IsMarten) -->
```sql
DELETE FROM mt_doc_refreshtoken;
```
<!--#else -->
```sql
DELETE FROM "RefreshTokens";
```
<!--#endif -->

Everyone then logs in again. Their passwords are unaffected: the key never touches them.
