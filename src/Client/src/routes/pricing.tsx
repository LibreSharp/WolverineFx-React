import type { PageHandle } from '../components/PageMeta'
import { PricingPage } from '../features/billing/PricingPage'

// Public: search engines may index it, and the build pre-renders it to HTML (pages.ts).
export const handle: PageHandle = { title: 'Pricing', index: true }

export default PricingPage
