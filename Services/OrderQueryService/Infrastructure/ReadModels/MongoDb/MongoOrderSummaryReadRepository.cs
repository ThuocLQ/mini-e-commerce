using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderQueryService.Application.Abstractions;
using OrderQueryService.Application.ReadModels;

namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoOrderSummaryReadRepository : IOrderSummaryReadRepository
{
    private readonly IMongoCollection<OrderSummaryDocument> _collection;

    public MongoOrderSummaryReadRepository(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _collection = database.GetCollection<OrderSummaryDocument>(
            mongoOptions.OrderSummariesCollectionName);
    }

    public async Task UpsertAsync(
        OrderSummaryReadModel model,
        CancellationToken cancellationToken = default)
    {
        var id = model.OrderId.ToString("D");
        var filter = Builders<OrderSummaryDocument>.Filter.Eq(x => x.Id, id);
        var existingDocument = await _collection
            .Find(filter)
            .Project<OrderSummaryDocument>(
                Builders<OrderSummaryDocument>.Projection
                    .Include(x => x.Id)
                    .Include(x => x.OrderId)
                    .Include(x => x.CustomerId)
                    .Include(x => x.CustomerName)
                    .Include(x => x.Status)
                    .Include(x => x.TotalAmount)
                    .Include(x => x.Currency)
                    .Include(x => x.ItemCount)
                    .Include(x => x.Items)
                    .Include(x => x.CreatedAtUtc)
                    .Include(x => x.LastUpdatedAtUtc)
                    .Include(x => x.PaidAtUtc)
                    .Include(x => x.CancelledAtUtc)
                    .Include(x => x.LastProjectedEventId)
                    .Include(x => x.LastProjectedEventType)
                    .Include(x => x.LastProjectedEventOccurredAtUtc)
                    .Include(x => x.LastProjectedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        var document = OrderSummaryDocumentMapper.ToDocument(
            model,
            existingDocument?.CreatedAtUtc);

        await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<OrderSummaryReadModel?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var orderIdText = orderId.ToString("D");
        var filter = Builders<OrderSummaryDocument>.Filter.Eq(x => x.OrderId, orderIdText);

        var document = await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : OrderSummaryDocumentMapper.ToReadModel(document);
    }

    public async Task<IReadOnlyList<OrderSummaryReadModel>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var documents = await _collection
            .Find(Builders<OrderSummaryDocument>.Filter.Empty)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents
            .Select(OrderSummaryDocumentMapper.ToReadModel)
            .ToList();
    }
}
