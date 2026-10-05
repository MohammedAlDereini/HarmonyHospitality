using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyTypes.List
{
    public class GetPagedPropertyTypesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyTypeModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(PropertyTypeModel.DisplayOrder),
            nameof(PropertyTypeModel.Code),
            nameof(PropertyTypeModel.NameEn),
            nameof(PropertyTypeModel.NameAr),
            nameof(PropertyTypeModel.IsActive),
            nameof(PropertyTypeModel.CreatedDate),
            nameof(PropertyTypeModel.ModifiedDate),
        ];

        public GetPagedPropertyTypesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(PropertyTypeModel.DisplayOrder), SortOrder = eSortOrder.ASC },
                new SortOrderOption { MemberName = nameof(PropertyTypeModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedPropertyTypesQueryValidator : AbstractValidator<GetPagedPropertyTypesQuery>
        {
            public GetPagedPropertyTypesQueryValidator()
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
