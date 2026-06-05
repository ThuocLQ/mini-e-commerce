namespace ProjectionWorker.Application.Abstractions;

public interface IProjectionFailureStore
{
    Task SaveAsync(ProjectionFailure failure, CancellationToken cancellationToken = default);
}
