# Known Limitations

- Module 00 establishes the build and project baseline only. Product features, PostgreSQL persistence, authentication, imports, analytics and full UI flows are intentionally deferred to later module iterations.
- Module 01 completed Figma inspection through the custom `mcp__figma__` server and does not depend on the earlier Starter-plan-limited provider. Raster screenshot files are not stored yet because the custom server surface used in this session returns structured node metadata rather than screenshot exports.
- Module 02 implements local setup/unlock with a durable JSON profile store and PBKDF2-SHA256 password hashing. PostgreSQL-backed auth persistence, migrations and seed/demo credentials remain deferred to the database/persistence module.
- Module 03 introduces routed shell navigation and portfolio UI state, but cross-module data reload/invalidation on portfolio switch remains deferred until portfolio/assets/dashboard data services are implemented.
