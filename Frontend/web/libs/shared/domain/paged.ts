/** The platform's paged answer (PagedResult): the rows of one page and how many there are in all. */
export interface Paged<T> {
  readonly results: readonly T[];
  readonly totalCount: number;
}

/** The direction of one sort instruction, as Identity numbers it. */
export const ASCENDING = 1;
export const DESCENDING = 2;
export type SortDirection = typeof ASCENDING | typeof DESCENDING;

/** One sort instruction the platform accepts: the member name as Identity names it (PascalCase) and a direction. */
export interface SortOrder {
  readonly memberName: string;
  readonly sortOrder: SortDirection;
}

/** The request half: Identity accepts pageNumber from 1 and pageSize 1 to 100. */
export interface PageRequest {
  readonly pageNumber: number;
  readonly pageSize: number;
}
