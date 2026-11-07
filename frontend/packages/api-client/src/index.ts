export { createApiClient, type ApiClientOptions, type SessionPort } from './http'
export { ApiError, toApiError, toFormErrors, type ProblemDetails } from './errors'
export {
  IDEMPOTENCY_HEADER,
  createIdempotentAction,
  newIdempotencyKey,
  withIdempotency,
} from './idempotency'
export { createDemoUsersApi, createUsersApi, type User, type UsersApi } from './users'
export type * from './contracts'
export type { components, paths } from './generated/schema'
// Las apps usan estos tipos sin depender de axios directamente.
export type { AxiosAdapter, AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios'
export { AxiosError } from 'axios'
