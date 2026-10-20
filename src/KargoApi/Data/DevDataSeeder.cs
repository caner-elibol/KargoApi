using Microsoft.EntityFrameworkCore;

namespace KargoApi.Data
{
    public class DevDataSeeder
    {
        public static async Task SeedAsync(AppDbContext db, ILogger logger, int shipmentCount = 500_000)
        {
            if (await db.Branches.AnyAsync())
            {
                logger.LogInformation("Veritabanında veri var, seed atlanıyor");
                return;
            }

            logger.LogInformation("Seed başlıyor: şubeler ve {Count} gönderi", shipmentCount);
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

            await db.Database.ExecuteSqlRawAsync(BranchesSql);
            await db.Database.ExecuteSqlRawAsync(SampleShipmentsSql);
            await db.Database.ExecuteSqlRawAsync(GeneratedShipmentsSql, shipmentCount);
            await db.Database.ExecuteSqlRawAsync(RareStatusSql);

            var total = await db.Shipments.CountAsync();
            logger.LogInformation("Seed tamamlandı: {Total} gönderi", total);
        }

        private const string BranchesSql = """
        INSERT INTO dbo.Branches (Name, City, IsActive) VALUES
        (N'Kadıköy Şube',   N'İstanbul', 1),
        (N'Beşiktaş Şube',  N'İstanbul', 1),
        (N'Çankaya Şube',   N'Ankara',   1),
        (N'Karşıyaka Şube', N'İzmir',    1),
        (N'Bornova Şube',   N'İzmir',    0),
        (N'Konak Şube',     N'İzmir',    1);
        """;

        private const string SampleShipmentsSql = """
        INSERT INTO dbo.Shipments
        (TrackingNo, SenderName, ReceiverCity, OriginBranchId, Status, WeightKg, CreatedAt, DeliveredAt) VALUES
        (N'SK1001', N'Ahmet Yılmaz',  N'Ankara',   1, 4,  2.50, '2026-10-01 09:15', '2026-10-03 14:20'),
        (N'SK1002', N'Ayşe Kaya',     N'İzmir',    1, 4,  0.80, '2026-10-01 11:40', '2026-10-02 16:05'),
        (N'SK1003', N'Mehmet Demir',  N'İstanbul', 3, 2, 12.00, '2026-10-02 10:00', NULL),
        (N'SK1004', N'Zeynep Şahin',  N'Bursa',    2, 3,  1.20, '2026-10-02 13:30', NULL),
        (N'SK1005', N'Can Öztürk',    N'Antalya',  4, 5,  5.75, '2026-10-03 08:45', NULL),
        (N'SK1006', N'Elif Aydın',    N'İstanbul', 4, 4,  3.10, '2026-10-03 15:10', '2026-10-05 10:30'),
        (N'SK1007', N'Burak Çelik',   N'Ankara',   2, 1, 25.00, '2026-10-04 09:00', NULL),
        (N'SK1008', N'Selin Arslan',  N'İzmir',    3, 4,  0.50, '2026-10-04 12:25', '2026-10-06 11:00'),
        (N'SK1009', N'Emre Koç',      N'Trabzon',  1, 2,  7.40, '2026-10-05 10:50', NULL),
        (N'SK1010', N'Deniz Kurt',    N'İstanbul', 5, 4,  2.00, '2026-10-05 14:15', '2026-10-07 09:45'),
        (N'SK1011', N'Ahmet Yılmaz',  N'İzmir',    1, 3,  1.75, '2026-10-06 09:30', NULL),
        (N'SK1012', N'Gizem Özdemir', N'Ankara',   2, 4,  4.20, '2026-10-06 16:40', '2026-10-08 13:15'),
        (N'SK1013', N'Oğuz Polat',    N'Bursa',    3, 5,  9.90, '2026-10-07 11:05', NULL),
        (N'SK1014', N'Merve Yıldız',  N'İstanbul', 4, 2,  0.30, '2026-10-07 13:55', NULL),
        (N'SK1015', N'Kerem Aksoy',   N'İstanbul', 1, 4, 15.50, '2026-10-08 08:20', '2026-10-09 17:30'),
        (N'SK1016', N'İrem Erdoğan',  N'İzmir',    2, 1,  2.60, '2026-10-08 10:10', NULL),
        (N'SK1017', N'Tolga Güneş',   N'Ankara',   5, 3,  6.00, '2026-10-09 12:00', NULL),
        (N'SK1018', N'Ayşe Kaya',     N'Trabzon',  1, 2,  3.30, '2026-10-09 15:45', NULL),
        (N'SK1019', N'Hakan Tekin',   N'İstanbul', 3, 4,  1.10, '2026-10-10 09:35', '2026-10-11 12:50'),
        (N'SK1020', N'Ece Bulut',     N'Bursa',    4, 1, 20.00, '2026-10-10 14:20', NULL);
        """;

        private const string GeneratedShipmentsSql = """
        SET NOCOUNT ON;

        WITH n AS (
            SELECT TOP ({0}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
            FROM sys.all_objects AS a
            CROSS JOIN sys.all_objects AS b
        )
        INSERT INTO dbo.Shipments
            (TrackingNo, SenderName, ReceiverCity, OriginBranchId, Status, WeightKg, CreatedAt, DeliveredAt)
        SELECT
            N'TR' + RIGHT('00000000' + CAST(i AS VARCHAR(8)), 8),
            CHOOSE(i % 8 + 1, N'Ahmet Yılmaz', N'Ayşe Kaya', N'Mehmet Demir', N'Zeynep Şahin',
                              N'Can Öztürk', N'Elif Aydın', N'Burak Çelik', N'Selin Arslan'),
            CHOOSE(i % 6 + 1, N'İstanbul', N'Ankara', N'İzmir', N'Bursa', N'Antalya', N'Trabzon'),
            i % 6 + 1,
            x.st,
            CAST((i % 3000) / 100.0 + 0.10 AS DECIMAL(6,2)),
            x.ca,
            CASE WHEN x.st = 4 THEN DATEADD(HOUR, 30, x.ca) END
        FROM n
        CROSS APPLY (
            SELECT CASE WHEN i % 100 < 85 THEN 4
                        WHEN i % 100 < 90 THEN 2
                        WHEN i % 100 < 95 THEN 3
                        WHEN i % 100 < 98 THEN 1
                        ELSE 5 END AS st,
                   DATEADD(MINUTE, -((i * 37) % 525600), CAST('2026-10-19' AS DATETIME2)) AS ca
        ) AS x;
        """;

        private const string RareStatusSql = """
        UPDATE TOP (50) dbo.Shipments SET Status = 6 WHERE Status = 2;
        """;
    }
}
