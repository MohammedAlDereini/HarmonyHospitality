/** The HTTP statuses the platform answers with (architecture 11.2), named once and read everywhere. */
const NETWORK_FAILURE = 0;
const OK = 200;
const CREATED = 201;
const ACCEPTED = 202;
const BAD_REQUEST = 400;
const UNAUTHORIZED = 401;
const FORBIDDEN = 403;
const CONFLICT = 409;
const UNPROCESSABLE = 422;
const BAD_GATEWAY = 502;
const SERVICE_UNAVAILABLE = 503;
const GATEWAY_TIMEOUT = 504;

export const HttpStatus = {
  /** The browser could not reach the server at all. */
  network: NETWORK_FAILURE,
  ok: OK,
  created: CREATED,
  /** A business rule said no; the codes say which. */
  accepted: ACCEPTED,
  /** Field codes. */
  badRequest: BAD_REQUEST,
  /** The session is gone. */
  unauthorized: UNAUTHORIZED,
  forbidden: FORBIDDEN,
  /** With 000.06: the same intent is still running; otherwise a conflict. */
  conflict: CONFLICT,
  /** 000.07: the intent key was reused for another intent. */
  unprocessable: UNPROCESSABLE,
  badGateway: BAD_GATEWAY,
  serviceUnavailable: SERVICE_UNAVAILABLE,
  gatewayTimeout: GATEWAY_TIMEOUT,
} as const;
