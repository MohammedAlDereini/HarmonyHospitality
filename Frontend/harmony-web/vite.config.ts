import { existsSync, readFileSync } from 'node:fs'
import { defineConfig, type ServerOptions } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'

// https on localhost on purpose: the reset and invitation links Identity mails out are https, and tokens never travel in clear.
// Preferred: the ASP.NET dev certificate Windows already trusts, exported once with
//   dotnet dev-certs https --export-path .cert/localhost.pem --format PEM --no-password
// Fallback when .cert/ is missing: a self-signed certificate the browser will warn about.
const certFile = '.cert/localhost.pem'
const keyFile = '.cert/localhost.key'
const haveDevCert = existsSync(certFile) && existsSync(keyFile)

const server: ServerOptions = {
  port: 5173,
  strictPort: true,
  ...(haveDevCert ? { https: { cert: readFileSync(certFile), key: readFileSync(keyFile) } } : {}),
}

export default defineConfig({
  plugins: [react(), ...(haveDevCert ? [] : [basicSsl()])],
  server,
})
