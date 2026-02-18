// VisionDbContext.cs
// VisionDbContext实现
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Infrastructure.Data;

/// <summary>
/// 视觉检测数据库上下文
/// </summary>
public class VisionDbContext : DbContext
{
    public VisionDbContext(DbContextOptions<VisionDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 工程表
    /// </summary>
    public DbSet<Project> Projects { get; set; } = null!;

    /// <summary>
    /// 算子表
    /// </summary>
    public DbSet<Operator> Operators { get; set; } = null!;

    /// <summary>
    /// 检测结果表
    /// </summary>
    public DbSet<InspectionResult> InspectionResults { get; set; } = null!;

    /// <summary>
    /// 缺陷表
    /// </summary>
    public DbSet<Defect> Defects { get; set; } = null!;

    /// <summary>
    /// 用户表
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 Project 实体
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.GlobalSettings).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.LastOpenedAt);



            // 配置 Table Splitting: Project 与 OperatorFlow 共享 Projects 表
            entity.HasOne(e => e.Flow)
                .WithOne()
                .HasForeignKey<OperatorFlow>(f => f.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("Projects");
        });


        // 配置 OperatorFlow 实体 (Table Splitting Part 2)
        modelBuilder.Entity<OperatorFlow>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(e => e.Id);

            // 映射属性到指定列名 (保持与原来 OwnsOne 的命名习惯兼容)
            entity.Property(e => e.Name).HasColumnName("Flow_Name").IsRequired().HasMaxLength(200);

            // 解决共享列冲突：映射到不同列
            entity.Property(e => e.CreatedAt).HasColumnName("Flow_CreatedAt");
            entity.Property(e => e.ModifiedAt).HasColumnName("Flow_ModifiedAt");
            entity.Property(e => e.IsDeleted).HasColumnName("Flow_IsDeleted");

            // 配置与 Operator 的关系
            entity.HasMany(e => e.Operators)
                .WithOne()
                .HasForeignKey(o => o.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // 配置 Connections (Owned Collection)
            entity.OwnsMany(e => e.Connections, connection =>
            {
                connection.HasKey("Id");
                connection.Property(c => c.SourceOperatorId).IsRequired();
                connection.Property(c => c.SourcePortId).IsRequired();
                connection.Property(c => c.TargetOperatorId).IsRequired();
                connection.Property(c => c.TargetPortId).IsRequired();
            });
        });
        // 配置 Operator 实体
        modelBuilder.Entity<Operator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.ExecutionStatus).IsRequired();

            // 配置 Position 为 Owned Entity
            entity.OwnsOne(e => e.Position, position =>
            {
                position.Property(p => p.X).HasColumnName("PositionX");
                position.Property(p => p.Y).HasColumnName("PositionY");
            });

            // 配置 InputPorts 为 Owned Entity Collection
            entity.OwnsMany(e => e.InputPorts, port =>
            {
                port.HasKey("Id");
                port.Property(p => p.Name).IsRequired().HasMaxLength(100);
                port.Property(p => p.Direction).IsRequired();
                port.Property(p => p.DataType).IsRequired();
                port.Property(p => p.IsRequired).IsRequired();
            });

            // 配置 OutputPorts 为 Owned Entity Collection
            entity.OwnsMany(e => e.OutputPorts, port =>
            {
                port.HasKey("Id");
                port.Property(p => p.Name).IsRequired().HasMaxLength(100);
                port.Property(p => p.Direction).IsRequired();
                port.Property(p => p.DataType).IsRequired();
                port.Property(p => p.IsRequired).IsRequired();
            });

            // 配置 Parameters 为 Owned Entity Collection
            entity.OwnsMany(e => e.Parameters, param =>
            {
                param.HasKey("Id");
                param.Property(p => p.Name).IsRequired().HasMaxLength(100);
                param.Property(p => p.DisplayName).HasMaxLength(200);
                param.Property(p => p.Description).HasMaxLength(1000);
                param.Property(p => p.DataType).IsRequired().HasMaxLength(50);
                param.Property(p => p.DefaultValueJson).HasMaxLength(4000);
                param.Property(p => p.ValueJson).HasMaxLength(4000);
                param.Property(p => p.MinValueJson).HasMaxLength(1000);
                param.Property(p => p.MaxValueJson).HasMaxLength(1000);
                param.Property(p => p.IsRequired).IsRequired();
            });
        });

        // 配置 InspectionResult 实体
        modelBuilder.Entity<InspectionResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.ProcessingTimeMs).IsRequired();
            entity.Property(e => e.InspectionTime).IsRequired();
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.InspectionTime);
            entity.HasIndex(e => e.Status);
        });

        // 配置 Defect 实体
        modelBuilder.Entity<Defect>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.X).IsRequired();
            entity.Property(e => e.Y).IsRequired();
            entity.Property(e => e.Width).IsRequired();
            entity.Property(e => e.Height).IsRequired();
            entity.Property(e => e.ConfidenceScore).IsRequired();
            entity.HasIndex(e => e.InspectionResultId);
        });

        // 配置 User 实体
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.LastLoginAt);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });
    }
}
