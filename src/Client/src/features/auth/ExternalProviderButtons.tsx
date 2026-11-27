import { useAuthOptions } from './authQueries'

// "Continue with Google/GitHub" for each provider the API has configured. These are plain links:
// signing in with a provider is a full-page round trip (see ExternalLoginEndpoints in the API).
export function ExternalProviderButtons() {
  const options = useAuthOptions()
  const providers = options.data?.externalProviders ?? []
  if (providers.length === 0) return null

  return (
    <div className="mx-auto flex max-w-sm flex-col gap-2 px-8">
      <p className="text-center text-sm text-gray-500">or</p>
      {providers.map((provider) => (
        <a
          key={provider.name}
          href={`/api/auth/external/${encodeURIComponent(provider.name)}`}
          className="rounded border border-gray-300 bg-white px-4 py-2 text-center text-sm hover:bg-gray-50"
        >
          Continue with {provider.displayName}
        </a>
      ))}
    </div>
  )
}
