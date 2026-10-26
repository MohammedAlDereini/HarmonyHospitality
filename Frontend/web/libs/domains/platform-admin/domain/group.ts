/** The brand's own columns (BrandDetailModel); null on every other type. */
export interface BrandDetail {
  readonly themeReference: string | null;
  readonly logoFileId: string | null;
  readonly websiteUrl: string | null;
  readonly franchisorName: string | null;
  readonly agreementStartsOn: string | null;
  readonly agreementEndsOn: string | null;
  readonly royaltyPercent: number | null;
}

/** One hotel's membership (PropertyGroupMemberModel); current ones first. */
export interface GroupMember {
  readonly id: string;
  readonly propertyId: string;
  readonly propertyCode: string;
  readonly propertyNameEn: string;
  readonly propertyNameAr: string;
  readonly fromUtc: string;
  readonly toUtc: string | null;
  readonly isCurrent: boolean;
}

/** One row of POST /api/PropertyGroup/get-all and GET /api/PropertyGroup/{id} (PropertyGroupModel). */
export interface PropertyGroup {
  readonly id: string;
  readonly tenantId: string;
  readonly typeCode: string;
  readonly code: string;
  readonly externalIdentifier: string | null;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly description: string | null;
  readonly precedence: number;
  readonly displayOrder: number;
  readonly isActive: boolean;
  readonly currentMembers: number;
  readonly brand: BrandDetail | null;
  readonly members: readonly GroupMember[];
}

/** The group's name in the person's language. */
export const groupTitle = (
  group: Pick<PropertyGroup, 'nameEn' | 'nameAr'>,
  locale: string,
): string => (locale.startsWith('ar') && group.nameAr !== '' ? group.nameAr : group.nameEn);

/** The type code the schema names; a group of it carries brand detail. */
export const BRAND_TYPE_CODE = 'Brand';
export const isBrandType = (typeCode: string): boolean =>
  typeCode.toLowerCase() === BRAND_TYPE_CODE.toLowerCase();

/** What the editors send for the brand (BrandDetailRequest); blanks become nulls in the store. */
export interface BrandDraft {
  readonly themeReference: string;
  readonly websiteUrl: string;
  readonly franchisorName: string;
  readonly agreementStartsOn: string;
  readonly agreementEndsOn: string;
  readonly royaltyPercent: number | null;
}

/** What the group creator writes (CreatePropertyGroupCommand). */
export interface GroupDraft {
  readonly tenantId: string;
  readonly typeCode: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly description: string;
  readonly precedence: number;
  readonly displayOrder: number;
  readonly externalIdentifier: string;
  readonly brand: BrandDraft | null;
}

/** What the group editor changes afterwards (UpdatePropertyGroupCommand). Never the code or the type. */
export interface GroupUpdate {
  readonly nameEn: string;
  readonly nameAr: string;
  readonly description: string;
  readonly precedence: number;
  readonly displayOrder: number;
  readonly externalIdentifier: string;
  readonly isActive: boolean;
  readonly brand: BrandDraft | null;
}

/** What the group list is filtered by; page is 1-based. */
export interface GroupFilters {
  readonly tenantId: string | null;
  readonly typeCode: string | null;
  readonly search: string;
  readonly page: number;
}
