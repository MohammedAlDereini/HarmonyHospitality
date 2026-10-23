import { ApiResult } from '@harmony/platform/http';

/** Every failure a person can see is one of these (architecture 14); 'unexpected' is a thrown error nobody handled. */
export type ErrorKind = Exclude<ApiResult<unknown>['kind'], 'ok'> | 'unexpected';
