IF OBJECT_ID(N'dbo.Motorcycles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Motorcycles
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Motorcycles PRIMARY KEY,
        Manufacturer NVARCHAR(100) NOT NULL,
        Model NVARCHAR(100) NOT NULL,
        [Year] INT NOT NULL,
        EngineCC INT NOT NULL,
        Price DECIMAL(12, 2) NOT NULL,
        VehicleType NVARCHAR(50) NULL,
        MotorcycleType NVARCHAR(50) NOT NULL,
        RiderExperience NVARCHAR(50) NULL,
        [Usage] NVARCHAR(100) NULL,
        Reliability NVARCHAR(30) NULL,
        MaintenanceCost NVARCHAR(30) NULL,
        Description NVARCHAR(1000) NULL,
        ImageUrl NVARCHAR(2048) NULL,
        CONSTRAINT CK_Motorcycles_Year CHECK ([Year] BETWEEN 1885 AND 2200),
        CONSTRAINT CK_Motorcycles_EngineCC CHECK (EngineCC BETWEEN 0 AND 10000),
        CONSTRAINT CK_Motorcycles_Price CHECK (Price >= 0),
        CONSTRAINT UQ_Motorcycles_Manufacturer_Model_Year UNIQUE (Manufacturer, Model, [Year])
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Motorcycles_Search' AND object_id = OBJECT_ID(N'dbo.Motorcycles'))
BEGIN
    CREATE INDEX IX_Motorcycles_Search
        ON dbo.Motorcycles (Price, EngineCC, [Year]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Motorcycles)
BEGIN
    INSERT INTO dbo.Motorcycles
        (Manufacturer, Model, [Year], EngineCC, Price, VehicleType, MotorcycleType, RiderExperience, [Usage], Reliability, MaintenanceCost, Description)
    VALUES
        (N'Honda', N'CB125F', 2023, 124, 18000, N'Motorcycle', N'Naked', N'Beginner', N'Commuting', N'High', N'Low', N'Illustrative sample data; price is for demonstration only.'),
        (N'Yamaha', N'MT-07', 2022, 689, 52000, N'Motorcycle', N'Naked', N'Intermediate', N'Commuting', N'High', N'Medium', N'Illustrative sample data; price is for demonstration only.'),
        (N'Kawasaki', N'Versys 650', 2021, 649, 61000, N'Motorcycle', N'Adventure', N'Intermediate', N'Touring', N'Medium', N'Medium', N'Illustrative sample data; price is for demonstration only.'),
        (N'Royal Enfield', N'Himalayan', 2024, 452, 39000, N'Motorcycle', N'Adventure', N'Beginner', N'Off-road', N'Medium', N'Low', N'Illustrative sample data; price is for demonstration only.');
END;
GO
