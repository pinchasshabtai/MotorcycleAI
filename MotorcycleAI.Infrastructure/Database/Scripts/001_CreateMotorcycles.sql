/*
    Run this script first.
    It creates the MotorcycleAI database and Motorcycles table only.
    Run 002_SeedIsraeliMotorcycles.sql afterwards to insert sample values.
*/

IF DB_ID(N'MotorcycleAI') IS NULL
BEGIN
    EXEC (N'CREATE DATABASE [MotorcycleAI]');
END;
GO

USE [MotorcycleAI];
GO

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
        PriceIsEstimate BIT NOT NULL CONSTRAINT DF_Motorcycles_PriceIsEstimate DEFAULT (0),
        AttributesAreEstimated BIT NOT NULL CONSTRAINT DF_Motorcycles_AttributesAreEstimated DEFAULT (0),
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
