import { api } from './api/client'

// Downloads a file from the API as the signed-in user (the browser can't add the access token to
// a plain link), and saves it under the name the API gives it.
export async function downloadFile(path: '/api/account/export' | '/api/organisation/export', fallbackName: string) {
  const { data, response } = await api.GET(path, { parseAs: 'blob' })
  if (!response.ok || !data) throw new Error('Could not download the file. Try again in a minute.')
  const name = /filename="?([^";]+)"?/.exec(response.headers.get('Content-Disposition') ?? '')?.[1] ?? fallbackName
  const url = URL.createObjectURL(data)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}
