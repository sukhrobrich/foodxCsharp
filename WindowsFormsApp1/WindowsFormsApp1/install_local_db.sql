-- ============================================================
-- FoodX — Mahalliy (oflayn) database sxemasi
-- Maqsad: Internet bo'lmaganda mahalliy SQL Server da ishlash
--
-- Ishlatish:
--   sqlcmd -S .\SQLEXPRESS -E -i install_local_db.sql
--   yoki SQL Server Management Studio da ochib F5
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'FoodX')
BEGIN
    CREATE DATABASE FoodX;
    PRINT 'FoodX databazasi yaratildi';
END
ELSE
    PRINT 'FoodX databazasi allaqachon mavjud';
GO

USE FoodX;
GO

-- ── Jadvallar (tartib: parent → child) ──────────────────────────────────

IF OBJECT_ID('dbo.settings','U') IS NULL
CREATE TABLE settings (
    [key]      NVARCHAR(100) NOT NULL,
    value      NVARCHAR(MAX) NULL,
    tenant_id  INT           NOT NULL DEFAULT 0,
    CONSTRAINT PK_settings PRIMARY KEY ([key], tenant_id)
);
-- Eski jadvalga tenant_id qo'shish (mavjud bazalar uchun)
IF EXISTS(SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID(N'settings') AND type=N'U')
   AND NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='settings' AND COLUMN_NAME='tenant_id')
BEGIN
    ALTER TABLE settings ADD tenant_id INT NOT NULL DEFAULT 0;
    -- Eski PRIMARY KEY ni olib tashlab composite qo'yamiz
    DECLARE @pk NVARCHAR(200) = (SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_NAME='settings' AND CONSTRAINT_TYPE='PRIMARY KEY');
    IF @pk IS NOT NULL EXEC('ALTER TABLE settings DROP CONSTRAINT ['+@pk+']');
    ALTER TABLE settings ADD CONSTRAINT PK_settings PRIMARY KEY ([key], tenant_id);
END

IF OBJECT_ID('dbo.user_category','U') IS NULL
CREATE TABLE user_category (
    id        INT IDENTITY(1,1) PRIMARY KEY,
    name      NVARCHAR(100) NOT NULL,
    role_type NVARCHAR(20)  NULL,
    color     NVARCHAR(20)  NULL
);

IF OBJECT_ID('dbo.[user]','U') IS NULL
CREATE TABLE [user] (
    id               INT IDENTITY(1,1) PRIMARY KEY,
    name             NVARCHAR(100) NOT NULL,
    user_category_id INT NOT NULL REFERENCES user_category(id),
    login            NVARCHAR(50)  NOT NULL,
    password         NVARCHAR(255) NOT NULL,
    phone_number     NVARCHAR(20)  NULL,
    created_at       DATETIME      NOT NULL DEFAULT GETDATE(),
    updated_at       DATETIME      NULL,
    sort_order       INT           NULL
);

IF OBJECT_ID('dbo.food_category','U') IS NULL
CREATE TABLE food_category (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    name         NVARCHAR(100) NOT NULL,
    printer_name NVARCHAR(200) NULL,
    sort_order   INT           NULL
);

IF OBJECT_ID('dbo.food','U') IS NULL
CREATE TABLE food (
    id               INT IDENTITY(1,1) PRIMARY KEY,
    food_category_id INT           NOT NULL REFERENCES food_category(id),
    name             NVARCHAR(100) NOT NULL,
    count            INT           NOT NULL,
    original_price   DECIMAL(10,2) NOT NULL,
    selling_price    DECIMAL(10,2) NOT NULL,
    photo            NVARCHAR(255) NULL,
    created_at       DATETIME      NULL DEFAULT GETDATE(),
    updated_at       DATETIME      NULL,
    unit             NVARCHAR(50)  NOT NULL DEFAULT 'dona',
    description      NVARCHAR(500) NULL,
    is_unlimited     BIT           NOT NULL DEFAULT 0,
    sort_order       INT           NULL
);

IF OBJECT_ID('dbo.place_category','U') IS NULL
CREATE TABLE place_category (
    id   INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(100) NOT NULL
);

IF OBJECT_ID('dbo.place_out','U') IS NULL
CREATE TABLE place_out (
    id                INT IDENTITY(1,1) PRIMARY KEY,
    place_category_id INT           NOT NULL REFERENCES place_category(id),
    name              NVARCHAR(100) NOT NULL,
    place_count       INT           NOT NULL,
    created_at        DATETIME      NULL DEFAULT GETDATE(),
    updated_at        DATETIME      NULL,
    serviceFee        VARCHAR(10)   NOT NULL DEFAULT 'YES',
    price             DECIMAL(18,2) NULL,
    sort_order        INT           NULL
);

IF OBJECT_ID('dbo.place_in','U') IS NULL
CREATE TABLE place_in (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    place_out_id INT          NULL REFERENCES place_out(id),
    room_name    VARCHAR(255) NULL,
    empty        VARCHAR(10)  NOT NULL DEFAULT 'YES',
    created_at   DATETIME     NOT NULL DEFAULT GETDATE(),
    user_id      INT          NULL,
    price        DECIMAL(18,2) NULL
);

IF OBJECT_ID('dbo.payment','U') IS NULL
CREATE TABLE payment (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    name       NVARCHAR(50) NOT NULL,
    sort_order INT NULL
);

-- customer — sync_token (oflayn yaratilganlar uchun) + central_id (sinxrondan keyin)
IF OBJECT_ID('dbo.customer','U') IS NULL
CREATE TABLE customer (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    name        NVARCHAR(200) NOT NULL,
    phone       NVARCHAR(50)  NULL,
    notes       NVARCHAR(500) NULL,
    created_at  DATETIME      NULL DEFAULT GETDATE(),
    is_synced   BIT           NOT NULL DEFAULT 0,
    sync_token  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    central_id  INT           NULL
);

-- [order] — sinxronizatsiya maydonlari bilan
IF OBJECT_ID('dbo.[order]','U') IS NULL
CREATE TABLE [order] (
    id               INT IDENTITY(1,1) PRIMARY KEY,
    user_id          INT            NOT NULL REFERENCES [user](id),
    place_id         INT            NOT NULL REFERENCES place_in(id),
    payment_id       INT            NULL REFERENCES payment(id),
    created_at       DATETIME       NULL DEFAULT GETDATE(),
    paid             NVARCHAR(20)   NOT NULL DEFAULT 'no',
    total            DECIMAL(18,2)  NOT NULL DEFAULT 0,
    discount_amount  DECIMAL(18,2)  NULL DEFAULT 0,
    discount_pct     DECIMAL(5,2)   NULL DEFAULT 0,
    customer_id      INT            NULL REFERENCES customer(id),
    customer_name    NVARCHAR(200)  NULL,
    delivery_phone   NVARCHAR(50)   NULL,
    delivery_address NVARCHAR(500)  NULL,
    is_delivery      BIT            NULL DEFAULT 0,
    order_note       NVARCHAR(1000) NULL,
    custom_svc_fee   DECIMAL(18,2)  NULL,
    custom_svc_type  NVARCHAR(3)    NULL,
    payment2_id      INT            NULL REFERENCES payment(id),
    payment2_amount  DECIMAL(18,2)  NULL,
    is_synced        BIT            NOT NULL DEFAULT 0,
    sync_token       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    central_id       INT            NULL
);

IF OBJECT_ID('dbo.order_food','U') IS NULL
CREATE TABLE order_food (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    order_id   INT           NOT NULL REFERENCES [order](id),
    food_id    INT           NOT NULL REFERENCES food(id),
    quantity   INT           NOT NULL DEFAULT 1,
    note       NVARCHAR(500) NULL,
    is_synced  BIT           NOT NULL DEFAULT 0,
    sync_token UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.order_payments','U') IS NULL
CREATE TABLE order_payments (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    order_id   INT           NOT NULL REFERENCES [order](id),
    payment_id INT           NOT NULL REFERENCES payment(id),
    amount     DECIMAL(18,2) NOT NULL DEFAULT 0,
    is_synced  BIT           NOT NULL DEFAULT 0,
    sync_token UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.ingredient','U') IS NULL
CREATE TABLE ingredient (
    id             INT IDENTITY(1,1) PRIMARY KEY,
    name           NVARCHAR(100) NOT NULL,
    unit           NVARCHAR(20)  NOT NULL DEFAULT 'kg',
    quantity       DECIMAL(10,3) NULL DEFAULT 0,
    price_per_unit DECIMAL(18,2) NULL DEFAULT 0,
    min_quantity   DECIMAL(10,3) NULL DEFAULT 0
);

IF OBJECT_ID('dbo.recipe_ingredient','U') IS NULL
CREATE TABLE recipe_ingredient (
    id                   INT IDENTITY(1,1) PRIMARY KEY,
    food_id              INT           NOT NULL REFERENCES food(id),
    ingredient_id        INT           NOT NULL REFERENCES ingredient(id),
    quantity_per_portion DECIMAL(10,4) NOT NULL
);

IF OBJECT_ID('dbo.ingredient_purchase','U') IS NULL
CREATE TABLE ingredient_purchase (
    id             INT IDENTITY(1,1) PRIMARY KEY,
    ingredient_id  INT           NOT NULL REFERENCES ingredient(id),
    quantity       DECIMAL(10,3) NOT NULL,
    price_per_unit DECIMAL(18,2) NOT NULL,
    total_price    DECIMAL(18,2) NOT NULL,
    purchased_at   DATETIME      NULL DEFAULT GETDATE(),
    notes          NVARCHAR(200) NULL,
    is_synced      BIT           NOT NULL DEFAULT 0,
    sync_token     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.food_purchase','U') IS NULL
CREATE TABLE food_purchase (
    id             INT IDENTITY(1,1) PRIMARY KEY,
    food_id        INT           NOT NULL REFERENCES food(id),
    quantity       INT           NOT NULL DEFAULT 1,
    price_per_unit DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_price    DECIMAL(18,2) NOT NULL DEFAULT 0,
    purchased_at   DATETIME      NULL DEFAULT GETDATE(),
    notes          NVARCHAR(200) NULL,
    is_synced      BIT           NOT NULL DEFAULT 0,
    sync_token     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.cash_transaction','U') IS NULL
CREATE TABLE cash_transaction (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    type        VARCHAR(10)   NOT NULL,
    category    NVARCHAR(100) NOT NULL,
    amount      DECIMAL(18,2) NOT NULL,
    description NVARCHAR(500) NULL,
    created_by  INT           NULL,
    created_at  DATETIME      NOT NULL DEFAULT GETDATE(),
    is_synced   BIT           NOT NULL DEFAULT 0,
    sync_token  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.order_debt','U') IS NULL
CREATE TABLE order_debt (
    id           INT IDENTITY(1,1) PRIMARY KEY,
    order_id     INT           NOT NULL REFERENCES [order](id),
    debtor_name  NVARCHAR(200) NOT NULL,
    debtor_phone NVARCHAR(50)  NULL,
    debt_note    NVARCHAR(500) NULL,
    amount       DECIMAL(18,2) NOT NULL DEFAULT 0,
    created_at   DATETIME      NOT NULL DEFAULT GETDATE(),
    is_paid      BIT           NOT NULL DEFAULT 0,
    paid_at      DATETIME      NULL,
    is_synced    BIT           NOT NULL DEFAULT 0,
    sync_token   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

IF OBJECT_ID('dbo.order_cancellation_log','U') IS NULL
CREATE TABLE order_cancellation_log (
    id            INT IDENTITY(1,1) PRIMARY KEY,
    order_id      INT           NOT NULL,
    food_id       INT           NULL,
    food_name     NVARCHAR(255) NULL,
    food_category NVARCHAR(255) NULL,
    cancelled_qty INT           NOT NULL DEFAULT 1,
    cancelled_by  NVARCHAR(255) NULL,
    cancelled_at  DATETIME      NOT NULL DEFAULT GETDATE(),
    is_synced     BIT           NOT NULL DEFAULT 0,
    sync_token    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);

GO
PRINT '================================================';
PRINT 'FoodX mahalliy bazasi tayyor!';
PRINT 'sync_token: ikkilanmaslikni kafolatlaydi';
PRINT 'central_id: sinxrondan keyin saqlandi';
PRINT 'is_synced=1 online, 0=oflayn kutmoqda';
PRINT '================================================';

-- ── Oflayn trigger: Session.IsOnline=false bo'lsa is_synced=0 yoziladi ──
-- Bu triggerlar lokal bazada ishlatilmaydi — Windows Forms o'zi boshqaradi
-- (dbconnect.cs da offline holatda is_synced=0 parametri uzatiladi)
