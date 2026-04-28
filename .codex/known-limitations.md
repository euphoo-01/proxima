# Known Limitations

- Module 00 establishes the build and project baseline only. Product features, PostgreSQL persistence, authentication, imports, analytics and full UI flows are intentionally deferred to later module iterations.
- Module 01 completed Figma inspection through the custom `mcp__figma__` server and does not depend on the earlier Starter-plan-limited provider. Raster screenshot files are not stored yet because the custom server surface used in this session returns structured node metadata rather than screenshot exports.
- Module 02 implements local setup/unlock with a durable JSON profile store and PBKDF2-SHA256 password hashing. PostgreSQL-backed auth persistence, migrations and seed/demo credentials remain deferred to the database/persistence module.
- Module 03 introduces routed shell navigation and portfolio UI state, but cross-module data reload/invalidation on portfolio switch remains deferred until portfolio/assets/dashboard data services are implemented.
- Module 04 introduces a portfolio domain/application/infrastructure flow and shell CRUD behavior, but persistence is still JSON-backed. PostgreSQL/EF Core storage, archive filters in DB queries and cross-module data scoping with real assets/transactions remain deferred.
- Module 05 adds asset CRUD/search/sort/tag filtering and asset-details navigation from the assets table, but storage is still JSON-backed and notes are only obfuscated (base64 placeholder), not encrypted with a dedicated key-management flow yet.
- Hard-delete flow with transaction dependency checks is deferred until Module 06 transaction persistence is available; current module supports archive-only safe removal.
