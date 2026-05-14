using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Proxima.Infrastructure.Persistence;

#nullable disable

namespace Proxima.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ProximaDbContext))]
[Migration("20260515022000_EncodePasswordCredential")]
public partial class EncodePasswordCredential : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
              if to_regclass('public.users') is null then
                return;
              end if;

              if exists (
                select 1
                from information_schema.columns
                where table_schema = 'public'
                  and table_name = 'users'
                  and column_name = 'password_hash'
                  and data_type = 'bytea'
              ) then
                alter table public.users add column if not exists password_hash_encoded text not null default '';

                if exists (
                  select 1
                  from information_schema.columns
                  where table_schema = 'public'
                    and table_name = 'users'
                    and column_name = 'password_salt'
                ) and exists (
                  select 1
                  from information_schema.columns
                  where table_schema = 'public'
                    and table_name = 'users'
                    and column_name = 'password_iterations'
                ) then
                  update public.users
                  set password_hash_encoded = case
                    when password_hash is not null
                      and length(password_hash) > 0
                      and password_salt is not null
                      and length(password_salt) > 0
                      and password_iterations > 0
                    then '$pbkdf2-sha256$v=1$i='
                      || password_iterations::text
                      || '$'
                      || encode(password_salt, 'base64')
                      || '$'
                      || encode(password_hash, 'base64')
                    else ''
                  end;
                end if;

                alter table public.users drop column password_hash;
                alter table public.users rename column password_hash_encoded to password_hash;
              end if;

              alter table public.users alter column password_hash type text using password_hash::text;
              alter table public.users alter column password_hash set default '';
              alter table public.users alter column password_hash set not null;

              alter table public.users drop column if exists password_algorithm;
              alter table public.users drop column if exists password_salt;
              alter table public.users drop column if exists password_iterations;
              alter table public.users drop column if exists password_version;
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: split password metadata columns were replaced by a self-contained encoded hash.
    }
}
