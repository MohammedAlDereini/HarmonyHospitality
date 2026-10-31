/**
 * A platform person as the Audit log's actor filter names them (POST /api/UserAccount/get-all, UserAccountModel):
 * only what the filter reads.
 */
export interface ActorChoice {
  readonly id: string;
  readonly displayName: string;
}
