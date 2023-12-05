module events

open api

type EventManifest = 
    { 
        metadata: Metadata
        eventTime: int64
        action: string
        note: string
        reason: string
        reportingController: string
        reportingInstance: string
        ``type``: string
    }
    interface Manifest with 
        member this.metadata = this.metadata 

type IEventLogger =
    abstract Log: (string -> string -> string -> unit)
    abstract Warn: (string -> string -> string -> unit)