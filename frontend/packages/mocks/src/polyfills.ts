// Polyfills mínimos para entornos como React Native (Hermes) donde ciertas
// APIs Web requeridas por MSW (MessageEvent, BroadcastChannel, localStorage, etc.)
// no existen en globalThis al momento de evaluar el módulo.

const g = globalThis as Record<string, unknown>

if (typeof g.Event === 'undefined') {
  class PolyfillEvent {
    type: string
    bubbles: boolean
    cancelable: boolean
    defaultPrevented = false
    target: unknown = null
    currentTarget: unknown = null
    constructor(type: string, init?: { bubbles?: boolean; cancelable?: boolean }) {
      this.type = String(type)
      this.bubbles = Boolean(init?.bubbles)
      this.cancelable = Boolean(init?.cancelable)
    }
    preventDefault() {
      if (this.cancelable) this.defaultPrevented = true
    }
    stopPropagation() {}
    stopImmediatePropagation() {}
  }
  g.Event = PolyfillEvent
}

if (typeof g.EventTarget === 'undefined') {
  class PolyfillEventTarget {
    private listeners = new Map<string, Set<unknown>>()
    addEventListener(type: string, callback: unknown) {
      if (!callback) return
      let set = this.listeners.get(type)
      if (!set) {
        set = new Set()
        this.listeners.set(type, set)
      }
      set.add(callback)
    }
    removeEventListener(type: string, callback: unknown) {
      if (!callback) return
      this.listeners.get(type)?.delete(callback)
    }
    dispatchEvent(event: { type: string; defaultPrevented?: boolean }): boolean {
      const set = this.listeners.get(event.type)
      if (set) {
        for (const cb of [...set]) {
          if (typeof cb === 'function') {
            ;(cb as (e: unknown) => void).call(this, event)
          } else if (
            typeof cb === 'object' &&
            cb !== null &&
            'handleEvent' in cb &&
            typeof (cb as { handleEvent: unknown }).handleEvent === 'function'
          ) {
            ;(cb as { handleEvent: (e: unknown) => void }).handleEvent(event)
          }
        }
      }
      return !event.defaultPrevented
    }
  }
  g.EventTarget = PolyfillEventTarget
}

if (typeof g.MessageEvent === 'undefined') {
  const BaseEvent = g.Event as new (
    type: string,
    init?: { bubbles?: boolean; cancelable?: boolean },
  ) => object
  class PolyfillMessageEvent extends BaseEvent {
    data: unknown
    origin: string
    lastEventId: string
    source: unknown
    ports: readonly unknown[]
    constructor(
      type: string,
      init?: {
        data?: unknown
        origin?: string
        lastEventId?: string
        source?: unknown
        ports?: readonly unknown[]
        bubbles?: boolean
        cancelable?: boolean
      },
    ) {
      super(type, init)
      this.data = init?.data ?? null
      this.origin = init?.origin ?? ''
      this.lastEventId = init?.lastEventId ?? ''
      this.source = init?.source ?? null
      this.ports = init?.ports ?? []
    }
  }
  g.MessageEvent = PolyfillMessageEvent
}

if (typeof g.CustomEvent === 'undefined') {
  const BaseEvent = g.Event as new (
    type: string,
    init?: { bubbles?: boolean; cancelable?: boolean },
  ) => object
  class PolyfillCustomEvent<T = unknown> extends BaseEvent {
    detail: T
    constructor(type: string, init?: { detail?: T; bubbles?: boolean; cancelable?: boolean }) {
      super(type, init)
      this.detail = (init?.detail ?? null) as T
    }
  }
  g.CustomEvent = PolyfillCustomEvent
}

if (typeof g.BroadcastChannel === 'undefined') {
  const BaseEventTarget = g.EventTarget as new () => object
  class PolyfillBroadcastChannel extends BaseEventTarget {
    name: string
    onmessage: ((event: unknown) => void) | null = null
    onmessageerror: ((event: unknown) => void) | null = null
    constructor(name: string) {
      super()
      this.name = String(name)
    }
    postMessage(_message: unknown) {}
    close() {}
    unref() {}
  }
  g.BroadcastChannel = PolyfillBroadcastChannel
}

if (typeof g.WritableStream === 'undefined') {
  class PolyfillWritableStream {
    locked = false
    constructor(_underlyingSink?: unknown) {}
    abort() {
      return Promise.resolve()
    }
    close() {
      return Promise.resolve()
    }
    getWriter() {
      return {
        write: () => Promise.resolve(),
        close: () => Promise.resolve(),
        abort: () => Promise.resolve(),
        releaseLock: () => {},
      }
    }
  }
  g.WritableStream = PolyfillWritableStream
}

if (typeof g.ReadableStream === 'undefined') {
  class PolyfillReadableStream {
    locked = false
    constructor(_underlyingSource?: unknown) {}
    cancel() {
      return Promise.resolve()
    }
    getReader() {
      return {
        read: () => Promise.resolve({ done: true, value: undefined }),
        cancel: () => Promise.resolve(),
        releaseLock: () => {},
      }
    }
  }
  g.ReadableStream = PolyfillReadableStream
}

if (typeof g.TextEncoder === 'undefined') {
  class PolyfillTextEncoder {
    encoding = 'utf-8'
    encode(input = ''): Uint8Array {
      const utf8 = unescape(encodeURIComponent(input))
      const result = new Uint8Array(utf8.length)
      for (let i = 0; i < utf8.length; i++) {
        result[i] = utf8.charCodeAt(i)
      }
      return result
    }
  }
  g.TextEncoder = PolyfillTextEncoder
}

if (typeof g.TextDecoder === 'undefined') {
  class PolyfillTextDecoder {
    encoding = 'utf-8'
    decode(input?: ArrayBuffer | ArrayBufferView): string {
      if (!input) return ''
      const bytes =
        input instanceof Uint8Array
          ? input
          : ArrayBuffer.isView(input)
            ? new Uint8Array(input.buffer, input.byteOffset, input.byteLength)
            : new Uint8Array(input)
      let binary = ''
      for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]!)
      }
      try {
        return decodeURIComponent(escape(binary))
      } catch {
        return binary
      }
    }
  }
  g.TextDecoder = PolyfillTextDecoder
}

if (typeof g.localStorage === 'undefined') {
  const store = new Map<string, string>()
  g.localStorage = {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => {
      store.set(key, String(value))
    },
    removeItem: (key: string) => {
      store.delete(key)
    },
    clear: () => {
      store.clear()
    },
    key: (index: number) => Array.from(store.keys())[index] ?? null,
    get length() {
      return store.size
    },
  }
}

if (
  typeof Headers !== 'undefined' &&
  typeof (Headers.prototype as unknown as Record<string, unknown>).getSetCookie !== 'function'
) {
  ;(Headers.prototype as unknown as Record<string, unknown>).getSetCookie = function (
    this: Headers,
  ): string[] {
    const value = this.get('set-cookie')
    return value ? [value] : []
  }
}

if (typeof (Promise as unknown as Record<string, unknown>).withResolvers !== 'function') {
  ;(Promise as unknown as Record<string, unknown>).withResolvers = function <T>() {
    let resolve!: (value: T | PromiseLike<T>) => void
    let reject!: (reason?: unknown) => void
    const promise = new Promise<T>((res, rej) => {
      resolve = res
      reject = rej
    })
    return { promise, resolve, reject }
  }
}

if (
  typeof URL !== 'undefined' &&
  typeof (URL as unknown as Record<string, unknown>).canParse !== 'function'
) {
  ;(URL as unknown as Record<string, unknown>).canParse = function (
    url: string | URL,
    base?: string | URL,
  ): boolean {
    try {
      new URL(url as string, base as string | undefined)
      return true
    } catch {
      return false
    }
  }
}
