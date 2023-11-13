module locations

open api

type Sandbox = { flavour: string; validUntil: string }

type SandboxStatus =
    { provisioning: string
      last_attempt: string }



type SandboxSpecManifest =
    { spec: Sandbox
      status: SandboxStatus option
      metadata: Metadata }

    interface Manifest with
        member this.metadata = this.metadata

type SandboxStatusManifest =
    { status: SandboxStatus
      metadata: Metadata }

    interface Manifest with
        member this.metadata = this.metadata
