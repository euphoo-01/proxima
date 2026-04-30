insert into users(id, display_name, login, role, created_at, updated_at)
values ('11111111-1111-1111-1111-111111111111', 'Demo Investor', 'demo', 'PrivateInvestor', now(), now())
on conflict (id) do nothing;

insert into portfolios(id, owner_user_id, name, base_currency, is_archived, created_at, updated_at)
values ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'Demo Portfolio', 'USD', false, now(), now())
on conflict (id) do nothing;

insert into assets(id, portfolio_id, ticker, name, type, currency, quantity, average_buy_price, current_price, is_archived, created_at, updated_at)
values
('33333333-3333-3333-3333-333333333331', '22222222-2222-2222-2222-222222222222', 'AAPL', 'Apple', 'Stock', 'USD', 10, 150, 182, false, now(), now()),
('33333333-3333-3333-3333-333333333332', '22222222-2222-2222-2222-222222222222', 'BTC', 'Bitcoin', 'Crypto', 'USD', 0.2, 42000, 60000, false, now(), now()),
('33333333-3333-3333-3333-333333333333', '22222222-2222-2222-2222-222222222222', 'USD-CASH', 'Cash', 'Cash', 'USD', 1, 20000, 20000, false, now(), now())
on conflict (id) do nothing;

insert into transactions(id, portfolio_id, asset_id, type, trade_date, quantity, price, gross_amount, fee_amount, tax_amount, currency, is_archived)
values
('44444444-4444-4444-4444-444444444441', '22222222-2222-2222-2222-222222222222', '33333333-3333-3333-3333-333333333331', 'Buy', now() - interval '120 days', 10, 150, 1500, 1, 0, 'USD', false),
('44444444-4444-4444-4444-444444444442', '22222222-2222-2222-2222-222222222222', '33333333-3333-3333-3333-333333333332', 'Buy', now() - interval '90 days', 0.2, 42000, 8400, 3, 0, 'USD', false),
('44444444-4444-4444-4444-444444444443', '22222222-2222-2222-2222-222222222222', '33333333-3333-3333-3333-333333333331', 'Dividend', now() - interval '30 days', 0, 0, 42, 0, 0, 'USD', false)
on conflict (id) do nothing;

insert into asset_prices(id, asset_id, price, currency, timestamp)
values
('55555555-5555-5555-5555-555555555551', '33333333-3333-3333-3333-333333333331', 182, 'USD', now()),
('55555555-5555-5555-5555-555555555552', '33333333-3333-3333-3333-333333333332', 60000, 'USD', now())
on conflict (id) do nothing;

insert into goals(id, portfolio_id, title, target_amount, monthly_contribution, expected_annual_return_percent, is_archived, created_at, updated_at)
values ('66666666-6666-6666-6666-666666666661', '22222222-2222-2222-2222-222222222222', 'Retire', 100000, 1000, 8, false, now(), now())
on conflict (id) do nothing;

insert into tax_profiles(id, user_id, profile_kind, updated_at)
values ('77777777-7777-7777-7777-777777777771', '11111111-1111-1111-1111-111111111111', 'PhysicalPerson', now())
on conflict (id) do nothing;
