using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Modules.List
{
    public class GetPagedModulesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<ModuleModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(ModuleModel.Code),
            nameof(ModuleModel.NameEn),
            nameof(ModuleModel.OwnerService),
            nameof(ModuleModel.Level),
            nameof(ModuleModel.HasLimit),
            nameof(ModuleModel.CreatedDate),
            nameof(ModuleModel.ModifiedDate),
        ];

        public GetPagedModulesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(ModuleModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Only modules of this owner service. Empty: all.</summary>
        public string? OwnerService { get; set; }

        /// <summary>Only modules sold on this floor (Property or Tenant). Empty: all.</summary>
        public ModuleLevel? Level { get; set; }

        /// <summary>Matches the code or the name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public class GetPagedModulesQueryValidator : AbstractValidator<GetPagedModulesQuery>
        {
            public GetPagedModulesQueryValidator()
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

                this.RuleFor(e => e.OwnerService)
                    .MaximumLength(64).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.OwnerServiceTooLong)
                    .When(e => !string.IsNullOrWhiteSpace(e.OwnerService));

                this.RuleFor(e => e.Level!.Value)
                    .IsInEnum().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.LevelInvalid)
                    .When(e => e.Level.HasValue);
            }
        }
    }
}
