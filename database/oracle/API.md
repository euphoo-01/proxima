# Прикладные пакеты Oracle

`24_portfolios.sql` и `25_admin.sql` устанавливаются после PKG_AUTH и PKG_LEDGER. Спецификации в SQL — точные контракты: первый параметр везде `p_token VARCHAR2`, курсоры OUT SYS_REFCURSOR, идентификаторы входа RAW(16), моменты операций TIMESTAMP WITH TIME ZONE. Идентификаторы в курсорах возвращаются RAWTOHEX-строками. Ни один пакет не выдаёт EXECUTE автоматически.

Каждый публичный вызов сначала выполняет begin_request; actor извлекается из защищённой сессии, не передаётся клиентом. Финансовый доступ разрешён только PrivateInvestor/FinancialConsultant и только к своим неархивным портфелям. Admin использует отдельный пакет. Передавать client_label может только FinancialConsultant; это метка собственного портфеля консультанта, а не право читать чужие портфели.

## PKG_PORTFOLIOS

- `list_portfolios(token,result)` → portfolio_id,name,description,client_label,base_currency,is_archived,created_at,updated_at; возвращает собственные активные портфели.
- `create_portfolio(token,name,base_currency,description,client_label,id OUT)`; `update_portfolio(token,id,name,description,client_label)`; `archive_portfolio(token,id)`. Имя обязательно, смена базовой валюты существующего портфеля не поддерживается. До/после изменения метаданных сохраняются в аудит, включая client_label. Архивирование не удаляет историю.
- `list_assets(token,portfolio,result)` → asset_id,instrument_id,ticker,name,currency,quantity,average_buy_price,current_price,is_archived.
- `list_transactions(token,portfolio,result)` → transaction_id,asset_id,type,trade_date,quantity,price,gross_amount,currency,fee_amount,tax_amount,broker,external_id,operation_order.
- `post_cash(token,portfolio,type,time,amount,currency,fee,id OUT,operation_order DEFAULT 0)`; `post_trade(token,portfolio,instrument,type,time,quantity,price,fee,id OUT,operation_order DEFAULT 0)`.
- `update_transaction(token,transaction,time,quantity,price,gross,fee,operation_order DEFAULT NULL)`; `remove_transaction(token,transaction)`.
- `get_balances(token,portfolio,result)` и `get_realized_pnl(token,portfolio,result)` сохраняют контракт PKG_LEDGER из LEDGER.md.

Поддерживаются только Deposit, Withdrawal, Buy, Sell. Проверки истории, денежных остатков, количества, FIFO и аудит финансовых изменений остаются в PKG_LEDGER. Приложение не получает прямой EXECUTE на него. Владелец пакетов должен разрешать ledger-вызовы по защищённому current_user_id; совпадение клиентского actor ID не считается аутентификацией. Простые списки не берут блокировки FOR UPDATE; ledger-чтения сохраняют существующую блокировку портфеля.

## PKG_ADMIN

Каждый вызов требует актуальную роль Admin:

- `list_users(token,result)` → user_id,login,display_name,role,account_status,failed_unlock_attempts,created_at,updated_at; без password_hash.
- `set_role(token,user,role)` принимает только Admin/PrivateInvestor/FinancialConsultant. Изменение роли отзывает все сессии пользователя.
- `set_status(token,user,status)` принимает Active/Blocked. Блокировка отзывает все сессии; активация сбрасывает счётчик ошибок входа.
- `list_audit(token,offset,limit,result)` → audit_id,user_id,action,timestamp,metadata_json. Offset — целое >=0; limit — целое 1–100. Порядок timestamp DESC,id DESC.
- `list_archived_portfolios(token,result)` → portfolio_id,owner_user_id,login,name,description,client_label,base_currency,updated_at.
- `restore_portfolio(token,id)`; `delete_archived_portfolio(token,id)` разрешены только для архивного портфеля. Окончательное удаление требует отдельного явного подтверждения в будущем UI; вызов процедуры является подтверждённой командой.
- `get_statistics(token,result)` → user_count,active_user_count,active_portfolio_count,archived_portfolio_count,transaction_count,audit_count; это счётчики объектов, не финансовая аналитика.

Изменения роли/статуса сериализуются блокировкой строк Admin по id, затем повторно проверяют право вызывающего и целевого пользователя. Запрещено лишать последнего активного администратора роли или активности. Для корректности административные записи должны проходить через этот пакет; прямой SYS-DML способен обойти инвариант. Автоматическая блокировка после ошибочных входов — самостоятельная политика PKG_AUTH и может закрыть последнего администратора, поэтому восстановление доступа через доверенного оператора БД остаётся необходимым.

Удаление архивного портфеля удаляет его transactions, import_rows/import_batches, goals, portfolio_snapshots, asset_tags/assets и сам портфель. Пользователь, инструменты, теги и audit_log сохраняются. Аудит хранит ссылку на удалённый портфель в JSON и его метаданные; финансовые события, записанные через ledger, сохраняют до/после в собственных журнальных записях.

Все изменения используют savepoint и откатывают собственные данные при любой ошибке, включая ошибку аудита. COMMIT отсутствует: клиент явно завершает бизнес-транзакцию. Пока блокировка/изменение роли не зафиксированы, другие транзакции видят ранее зафиксированное состояние согласно Oracle ACID; после COMMIT старые токены отвергаются. Автономные вход/выход из PKG_AUTH нельзя вызывать, пока эта же клиентская транзакция удерживает незавершённые изменения соответствующего пользователя/сессии.

Коды прикладных ошибок: -20400 роль портфеля, -20401 недоступный портфель, -20402 метка без права консультанта, -20403 пустое имя; -20500 роль, -20501 последний администратор, -20502 статус, -20503 параметры страницы, -20504 требуется архив. Ошибки PKG_AUTH/PKG_LEDGER передаются без подмены.

Порядок финансовой истории — trade_date,operation_order,id. operation_order — явно заданное целое 0..999999999999999999; повтор пары (момент,порядок) внутри портфеля даёт -20103. У post_cash/post_trade последний необязательный параметр после OUT id равен 0; update_transaction при NULL сохраняет прежнее значение. list_transactions возвращает operation_order последним столбцом и сортирует по этой последовательности.
