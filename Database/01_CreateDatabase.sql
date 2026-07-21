-- Hardware POS: Create database
-- Run against: .\SQLEXPRESS (or your local SQL Server Express instance)
-- Order: 01 -> 02 -> 03

IF DB_ID(N'HardwarePOS') IS NULL
BEGIN
    CREATE DATABASE HardwarePOS;
END
GO
