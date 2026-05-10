create table if not exists notifications (
  id uuid primary key,
  user_id uuid not null references users(id) on delete cascade,
  severity varchar(32) not null,
  title varchar(120) not null,
  message varchar(2000) not null,
  source varchar(120) not null default '',
  created_at_utc timestamptz not null,
  deleted_at_utc timestamptz null
);

create index if not exists ix_notifications_user_deleted_created
  on notifications(user_id, deleted_at_utc, created_at_utc desc);
