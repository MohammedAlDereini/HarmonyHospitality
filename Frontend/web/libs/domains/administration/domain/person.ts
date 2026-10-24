/** One line of GET /api/UserAccount/{id}/security-events (SecurityEventModel): newest first, 100 at most. */
export interface SecurityEvent {
  readonly action: string;
  readonly occurredAtUtc: string;
  readonly actorSubject: string;
  readonly sourceAddress: string | null;
  readonly device: string | null;
  readonly detail: string | null;
}

/** One line of GET /api/UserAccount/{id}/devices (UserDeviceModel). */
export interface Device {
  readonly id: string;
  readonly label: string;
  readonly firstSeenUtc: string;
  readonly lastSeenUtc: string;
  readonly lastAddress: string | null;
  readonly signedIn: boolean;
}

/** One line of GET /api/UserAccount/{id}/passkeys (UserPasskeyModel); only what the screen shows. */
export interface Passkey {
  readonly id: string;
  readonly name: string;
  readonly createdUtc: string;
  readonly lastUsedUtc: string | null;
  readonly isBackedUp: boolean;
}
