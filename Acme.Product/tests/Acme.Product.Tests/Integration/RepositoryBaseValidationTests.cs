using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.Data;
using Acme.Product.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Tests.Integration;

public sealed class RepositoryBaseValidationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VisionDbContext _context;
    private readonly ProjectRepository _repository;

    public RepositoryBaseValidationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<VisionDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new VisionDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new ProjectRepository(_context);
    }

    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        Action act = () => new ProjectRepository(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public async Task FindAsync_NullPredicate_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _repository.FindAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    [Fact]
    public async Task AddAsync_NullEntity_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _repository.AddAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("entity");
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _repository.UpdateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("entity");
    }

    [Fact]
    public async Task DeleteAsync_NullEntity_ThrowsArgumentNullException()
    {
        Func<Task> act = async () => await _repository.DeleteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("entity");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
