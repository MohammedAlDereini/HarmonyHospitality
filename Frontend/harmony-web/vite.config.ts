import { existsSync, readFileSync } from 'node:fs'
import { defineConfig, type ServerOptions } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

// https on localhost on purpose: the session cookie is Secure with the __Host- prefix, and the reset and
// invitation links Identity mails out are https.
// Preferred: the ASP.NET dev certificate Windows already trusts, exported once with
//   dotnet dev-certs https --export-path .cert/localhost.pem --format PEM --no-password
// Fallback when .cert/ is missing: a self-signed certificate the browser will warn about.
const certFile = '.cert/localhost.pem'
const keyFile = '.cert/localhost.key'
const haveDevCert = existsSync(certFile) && existsSync(keyFile)

// Everything that belongs to the BFF goes to the BFF, on THIS origin: the browser sees one site and one cookie,
// exactly as in production, where the BFF serves the built app itself. The Host header stays localhost:5173 on
// purpose (no changeOrigin): that is the origin Identity registered, and the BFF builds its redirect from it.
const bff = 'https://localhost:5210'
const proxy = Object.fromEntries(
  ['/bff', '/api', '/signin-oidc', '/signout-callback-oidc', '/signout-oidc'].map((path) => [path, { target: bff, secure: false }]),
)

const server: ServerOptions = {
  port: 5173,
  strictPort: true,
  proxy,
  ...(haveDevCert ? { https: { cert: readFileSync(certFile), key: readFileSync(keyFile) } } : {}),
}

export default defineConfig({
  plugins: [react(), ...(haveDevCert ? [] : [basicSsl()])],
  server,
})
