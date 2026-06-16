using FluentValidation;
using MarketingAutomation.Modules.Segments.Domain;
using MarketingAutomation.Modules.Segments.Infrastructure;
using MarketingAutomation.SharedKernel.Application;
using MarketingAutomation.SharedKernel.Segments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Segments.Application;

// ---- Create -------------------------------------------------------------------------

public sealed record CreateSegmentCommand(string Name, SegmentType Type, SegmentGroup Definition)
    : IRequest<SegmentDto>;

public sealed class CreateSegmentValidator : AbstractValidator<CreateSegmentCommand>
{
    public CreateSegmentValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
}

public sealed class CreateSegmentHandler(SegmentsDbContext db) : IRequestHandler<CreateSegmentCommand, SegmentDto>
{
    public async Task<SegmentDto> Handle(CreateSegmentCommand request, CancellationToken ct)
    {
        var segment = new Segment { Name = request.Name, Type = request.Type, Definition = request.Definition };
        db.Segments.Add(segment);
        await db.SaveChangesAsync(ct);
        return SegmentDto.From(segment);
    }
}

// ---- Update -------------------------------------------------------------------------

public sealed record UpdateSegmentCommand(Guid Id, string Name, SegmentGroup Definition) : IRequest<SegmentDto>;

public sealed class UpdateSegmentHandler(SegmentsDbContext db) : IRequestHandler<UpdateSegmentCommand, SegmentDto>
{
    public async Task<SegmentDto> Handle(UpdateSegmentCommand request, CancellationToken ct)
    {
        var segment = await Load(db, request.Id, ct);
        segment.Name = request.Name;
        segment.Definition = request.Definition;
        segment.CachedCount = null; // definition changed; previous count is stale
        await db.SaveChangesAsync(ct);
        return SegmentDto.From(segment);
    }

    internal static async Task<Segment> Load(SegmentsDbContext db, Guid id, CancellationToken ct) =>
        await db.Segments.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("Segment", id);
}

// ---- Delete -------------------------------------------------------------------------

public sealed record DeleteSegmentCommand(Guid Id) : IRequest;

public sealed class DeleteSegmentHandler(SegmentsDbContext db) : IRequestHandler<DeleteSegmentCommand>
{
    public async Task Handle(DeleteSegmentCommand request, CancellationToken ct)
    {
        db.Segments.Remove(await UpdateSegmentHandler.Load(db, request.Id, ct));
        await db.SaveChangesAsync(ct);
    }
}

// ---- Get / List ---------------------------------------------------------------------

public sealed record GetSegmentQuery(Guid Id) : IRequest<SegmentDto>;

public sealed class GetSegmentHandler(SegmentsDbContext db) : IRequestHandler<GetSegmentQuery, SegmentDto>
{
    public async Task<SegmentDto> Handle(GetSegmentQuery request, CancellationToken ct)
    {
        var segment = await db.Segments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new NotFoundException("Segment", request.Id);
        return SegmentDto.From(segment);
    }
}

public sealed record ListSegmentsQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<SegmentSummaryDto>>;

public sealed class ListSegmentsHandler(SegmentsDbContext db)
    : IRequestHandler<ListSegmentsQuery, PagedResult<SegmentSummaryDto>>
{
    public async Task<PagedResult<SegmentSummaryDto>> Handle(ListSegmentsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.Segments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(s => s.Name.ToLower().Contains(term));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SegmentSummaryDto(s.Id, s.Name, s.Type, s.CachedCount, s.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<SegmentSummaryDto>(items, page, pageSize, total);
    }
}

// ---- Preview (live count + sample, no save) -----------------------------------------

public sealed record PreviewSegmentCommand(SegmentGroup Definition, int SampleSize = 10)
    : IRequest<SegmentPreviewDto>;

public sealed class PreviewSegmentHandler(ISegmentEvaluator evaluator)
    : IRequestHandler<PreviewSegmentCommand, SegmentPreviewDto>
{
    public async Task<SegmentPreviewDto> Handle(PreviewSegmentCommand request, CancellationToken ct)
    {
        var sampleSize = Math.Clamp(request.SampleSize, 0, 100);
        var sample = new List<SegmentMatch>();
        long count = 0;

        await foreach (var match in evaluator.EvaluateAsync(request.Definition, ct))
        {
            count++;
            if (sample.Count < sampleSize) sample.Add(match);
        }

        return new SegmentPreviewDto(count, sample);
    }
}

// ---- AI: natural language -> AST ----------------------------------------------------

public sealed record BuildSegmentFromTextCommand(string Description) : IRequest<SegmentGroup>;

public sealed class BuildSegmentFromTextValidator : AbstractValidator<BuildSegmentFromTextCommand>
{
    public BuildSegmentFromTextValidator() => RuleFor(c => c.Description).NotEmpty().MaximumLength(2000);
}

public sealed class BuildSegmentFromTextHandler(ISegmentAiBuilder builder)
    : IRequestHandler<BuildSegmentFromTextCommand, SegmentGroup>
{
    public Task<SegmentGroup> Handle(BuildSegmentFromTextCommand request, CancellationToken ct) =>
        builder.BuildAsync(request.Description, ct);
}
