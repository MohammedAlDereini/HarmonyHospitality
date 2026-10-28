import { ApiResult } from '@harmony/platform/http';

/** Identity's answer when a change waits for a second administrator (BusinessErrorCodes.Identity.Approval.SentForApproval). */
export const SENT_FOR_APPROVAL = '00131';

/** The change was not refused: it waits for a second administrator (stage D). */
export const sentForApproval = (result: ApiResult<unknown>): boolean =>
  result.kind === 'refused' && result.codes.includes(SENT_FOR_APPROVAL);

export const sentForApprovalToast = (): string =>
  $localize`:@@administration.approvals.toast.sent:Sent for approval: a second administrator decides.`;
