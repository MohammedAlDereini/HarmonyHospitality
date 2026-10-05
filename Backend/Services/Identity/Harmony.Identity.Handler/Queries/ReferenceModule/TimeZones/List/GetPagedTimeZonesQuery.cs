using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.TimeZones.List
{
    public class GetPagedTimeZonesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<TimeZoneModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(TimeZoneModel.Id),
            nameof(TimeZoneModel.NameEn),
            nameof(TimeZoneModel.NameAr),
            nameof(TimeZoneModel.IsActive),
            nameof(TimeZoneModel.CreatedDate),
            nameof(TimeZoneModel.ModifiedDate),
        ];

        public GetPagedTimeZonesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(TimeZoneModel.Id), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the id or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedTimeZonesQueryValidator : AbstractValidator<GetPagedTimeZonesQuery>
        {
            public GetPagedTimeZonesQueryValidator()
            {
                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

                this.RuleFor(e => e.PageSize)
                    .InclusiveBetween(1, 100)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);

                this.RuleForEach(e => e.SortOrder)
                    .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                    .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
            }
        }
    }
}
