/** What a screen needs to know about a property, in Identity's own words. */
export interface PropertySummary {
  /** Identity's id: what X-Harmony-Property carries on every call made inside the property. */
  readonly id: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  /** IANA zone, e.g. Asia/Riyadh: every instant of this property is shown in it. */
  readonly timeZoneId: string;
  /** ISO 4217, e.g. JOD. */
  readonly currency: string;
}

/** The rows Identity answers from POST /api/Property/mine (PropertyModel); only the fields the app reads. */
export interface PropertyRow {
  readonly id: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly timeZoneId: string;
  readonly defaultCurrency: string;
}

export const summaryOf = (row: PropertyRow): PropertySummary => ({
  id: row.id,
  code: row.code,
  nameEn: row.nameEn,
  nameAr: row.nameAr,
  timeZoneId: row.timeZoneId,
  currency: row.defaultCurrency,
});

/** The name in the build's language. It is user data: templates wrap it in <bdi>. */
export const propertyName = (property: PropertySummary, locale: string): string =>
  locale.startsWith('ar') ? property.nameAr : property.nameEn;
