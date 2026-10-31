/** One month of the onboarding chart: "2026-10" and a count (MonthCountModel). */
export interface MonthCount {
  readonly month: string;
  readonly count: number;
}

/** A customer named by the summary (TenantBriefModel). */
export interface TenantBrief {
  readonly id: string;
  readonly code: string;
  readonly legalNameEn: string;
  readonly legalNameAr: string;
  readonly statusReason: string | null;
  readonly modifiedDate: string | null;
}

/** A fixed-term subscription line that ends soon (EndingLineModel). */
export interface EndingLine {
  readonly tenantId: string;
  readonly tenantCode: string;
  readonly tenantNameEn: string;
  readonly tenantNameAr: string;
  readonly propertyId: string | null;
  readonly propertyCode: string | null;
  readonly moduleCode: string;
  readonly endsOn: string;
  readonly rooms: number | null;
}

/** The customers in numbers (GET /api/Tenant/summary, TenantSummaryModel). The platform's own row is never counted. */
export interface TenantSummary {
  readonly today: string;
  readonly active: number;
  readonly suspended: number;
  readonly terminated: number;
  readonly countries: number;
  readonly onboardedThisMonth: number;
  /** Twelve months, oldest first, the current month last. */
  readonly onboardedByMonth: readonly MonthCount[];
  readonly suspendedTenants: readonly TenantBrief[];
  readonly linesEndingSoon: readonly EndingLine[];
}

/** A hotel running more rooms than it licenses (PropertyLicenceModel). */
export interface PropertyLicence {
  readonly id: string;
  readonly tenantId: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly totalRooms: number;
  readonly licensedRooms: number;
}

/** The hotels in numbers (GET /api/Property/summary, PropertySummaryModel). */
export interface PropertySummary {
  readonly today: string;
  readonly total: number;
  readonly active: number;
  readonly openingSoon: number;
  readonly createdThisMonth: number;
  readonly totalRooms: number;
  readonly licensedRooms: number;
  readonly aboveLicence: number;
  readonly aboveLicenceProperties: readonly PropertyLicence[];
}
