import type { PageHandle } from '../components/PageMeta'
import { HomePage } from '../components/HomePage'

// Public: search engines may index it, and the build pre-renders it to HTML (pages.ts).
export const handle: PageHandle = { index: true }

export default HomePage
