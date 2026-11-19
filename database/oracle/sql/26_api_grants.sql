WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
GRANT EXECUTE ON proxima_owner.pkg_auth TO proxima_api_role;
GRANT EXECUTE ON proxima_owner.pkg_portfolios TO proxima_api_role;
GRANT EXECUTE ON proxima_owner.pkg_admin TO proxima_api_role;
GRANT EXECUTE ON proxima_owner.pkg_workspace TO proxima_api_role;
GRANT EXECUTE ON proxima_owner.pkg_csv TO proxima_api_role;
