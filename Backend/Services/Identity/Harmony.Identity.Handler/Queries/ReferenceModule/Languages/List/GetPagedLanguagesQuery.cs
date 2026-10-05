using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Languages.List
{
    public class GetPagedLanguagesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<LanguageModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(LanguageModel.Code),
            nameof(LanguageModel.NameEn),
            nameof(LanguageModel.NameNative),
            nameof(LanguageModel.IsRightToLeft),
            nameof(LanguageModel.IsActive),
            nameof(LanguageModel.CreatedDate),
            nameof(LanguageModel.ModifiedDate),
        ];

        public GetPagedLanguagesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(LanguageModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public bool? IsActive { get; set; }

        public class GetPagedLanguagesQueryValidator : AbstractValidator<GetPagedLanguagesQuery>
        {
            public GetPagedLanguagesQueryValidator()
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
