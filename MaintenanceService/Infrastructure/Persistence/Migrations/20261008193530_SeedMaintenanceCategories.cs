using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentCenter.MaintenanceService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedMaintenanceCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO [Categories] ([Id], [Name], [Description], [IsActive])
                SELECT defaults.[Id], defaults.[Name], defaults.[Description], CAST(1 AS bit)
                FROM (VALUES
                    ('b1833d4a-2447-441d-a324-000000000001', 'Plumbing', 'Leaks, taps, toilets and drainage.'),
                    ('b1833d4a-2447-441d-a324-000000000002', 'Electrical', 'Lights, sockets and electrical faults.'),
                    ('b1833d4a-2447-441d-a324-000000000003', 'Heating', 'Radiators and room heating.'),
                    ('b1833d4a-2447-441d-a324-000000000004', 'Furniture', 'Beds, desks, chairs and wardrobes.'),
                    ('b1833d4a-2447-441d-a324-000000000005', 'Doors and windows', 'Locks, handles, doors and windows.'),
                    ('b1833d4a-2447-441d-a324-000000000006', 'Other', 'Other room maintenance problems.')
                ) AS defaults ([Id], [Name], [Description])
                WHERE NOT EXISTS (
                    SELECT 1 FROM [Categories] existing WHERE existing.[Name] = defaults.[Name]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [Categories]
                WHERE [Id] IN (
                    'b1833d4a-2447-441d-a324-000000000001',
                    'b1833d4a-2447-441d-a324-000000000002',
                    'b1833d4a-2447-441d-a324-000000000003',
                    'b1833d4a-2447-441d-a324-000000000004',
                    'b1833d4a-2447-441d-a324-000000000005',
                    'b1833d4a-2447-441d-a324-000000000006'
                )
                AND NOT EXISTS (
                    SELECT 1 FROM [Requests] WHERE [CategoryId] = [Categories].[Id]
                );
                """);
        }
    }
}
