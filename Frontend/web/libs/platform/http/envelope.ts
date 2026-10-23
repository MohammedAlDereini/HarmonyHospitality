/** The platform's answer shape on every status: data on success, codes on refusal (architecture 11.2). */
export interface ApiError {
  code: string;
  message?: string;
  field?: string;
}

export interface Envelope<T> {
  data?: T;
  errors?: ApiError[];
}

/** The codes carried by an envelope, or none when the body is not one. */
export function codesOf(body: unknown): string[] {
  if (typeof body !== 'object' || body === null) return [];
  const errors = (body as Envelope<unknown>).errors;
  return Array.isArray(errors) ? errors.map((e) => e.code) : [];
}
