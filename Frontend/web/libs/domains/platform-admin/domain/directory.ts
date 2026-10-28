/** A group a customer's directory pushed, as the console shows it (DirectoryGroupModel). */
export interface TenantDirectoryGroup {
  readonly id: string;
  readonly displayName: string;
  /** Null: the group gives nothing. */
  readonly roleId: string | null;
  readonly members: number;
}

/** GET /api/Directory/of/{tenantId} (DirectoryModel): a customer's company directory; null data when it has none. */
export interface TenantDirectory {
  readonly id: string;
  readonly clientId: string;
  readonly isEnabled: boolean;
  readonly secretIssuedAtUtc: string;
  readonly people: number;
  readonly groups: readonly TenantDirectoryGroup[];
}
