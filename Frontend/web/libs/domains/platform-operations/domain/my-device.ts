/** A browser the signed-in person used (GET /api/UserAccount/MyDevices, UserDeviceModel). Never the device key. */
export interface MyDevice {
  readonly id: string;
  /** "Chrome on Windows", as Identity read the browser. */
  readonly label: string;
  readonly firstSeenUtc: string;
  readonly lastSeenUtc: string;
  readonly lastAddress: string | null;
  /** True while the session last opened on this device is still open. */
  readonly signedIn: boolean;
  /** When the person trusted this browser; null: not trusted. */
  readonly trustedAtUtc: string | null;
}
