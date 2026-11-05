// Genera src/generated/schema.d.ts desde el OpenAPI del backend .NET.
// Uso (desde frontend/):  pnpm gen:api
//   OPENAPI_URL=https://staging.ejemplo.pe/openapi/v1.json pnpm gen:api
import { writeFile } from 'node:fs/promises'
import openapiTS, { astToString } from 'openapi-typescript'

const source = process.env.OPENAPI_URL ?? 'http://localhost:5000/openapi/v1.json'
const target = new URL('../src/generated/schema.d.ts', import.meta.url)

const ast = await openapiTS(new URL(source))
const header = `// GENERADO desde ${source} — no editar a mano. Regenerar con: pnpm gen:api\n`
await writeFile(target, header + astToString(ast))
console.log(`Tipos generados en ${target.pathname} desde ${source}`)
