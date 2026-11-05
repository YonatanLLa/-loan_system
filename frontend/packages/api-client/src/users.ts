import type { AxiosInstance } from 'axios'

/** Usuario autenticado (contrato provisional de GET /me hasta tener el OpenAPI). */
export interface User {
  id: string
  fullName: string
  email: string
  role: string
}

export interface UsersApi {
  getCurrentUser(): Promise<User>
}

/** Cliente real: consulta al backend .NET. Los errores llegan como ApiError. */
export function createUsersApi(http: AxiosInstance): UsersApi {
  return {
    async getCurrentUser() {
      const { data } = await http.get<User & { permissions?: string[] }>('/me')
      const { id, fullName, email, role } = data
      return { id, fullName, email, role }
    },
  }
}

/** Versión sin backend para el modo demo: devuelve el usuario ficticio de la sesión. */
export function createDemoUsersApi(user: User): UsersApi {
  return {
    getCurrentUser: () => Promise.resolve({ ...user }),
  }
}
