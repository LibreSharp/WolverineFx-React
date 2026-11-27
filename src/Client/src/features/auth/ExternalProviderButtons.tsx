import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { useAuthOptions } from './authQueries'

// "Continue with Google/GitHub" for each provider the API has configured. These are plain links:
// signing in with a provider is a full-page round trip (see ExternalLoginEndpoints in the API).
export function ExternalProviderButtons() {
  const options = useAuthOptions()
  const providers = options.data?.externalProviders ?? []
  if (providers.length === 0) return null

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Separator className="flex-1" />
        or
        <Separator className="flex-1" />
      </div>
      {providers.map((provider) => (
        <Button key={provider.name} asChild variant="outline">
          <a href={`/api/auth/external/${encodeURIComponent(provider.name)}`}>Continue with {provider.displayName}</a>
        </Button>
      ))}
    </div>
  )
}
