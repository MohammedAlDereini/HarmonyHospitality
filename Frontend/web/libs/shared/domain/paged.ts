/** The platform's paged answer (PagedResult): the rows of one page and how many there are in all. */
export interface Paged<T> {
  readonly results: readonly T[];
  readonly totalCount: number;
}

/** The request half: Identity accepts pageNumber from 1 and pageSize 1 to 100. */
export interface PageRequest {
  readonly pageNumber: number;
  readonly pageSize: number;
}
