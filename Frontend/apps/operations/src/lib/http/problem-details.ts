export function problemMessage(value: unknown): string | null {
  if (typeof value === "string" && value.trim()) return value;
  if (!isRecord(value)) return null;

  for (const key of ["message", "Message", "error", "detail"]) {
    const message = value[key];
    if (typeof message === "string" && message.trim()) return message;
  }

  if (isRecord(value.errors)) {
    for (const messages of Object.values(value.errors)) {
      if (Array.isArray(messages)) {
        const first = messages.find((message): message is string => typeof message === "string" && message.trim().length > 0);
        if (first) return first;
      }

      if (typeof messages === "string" && messages.trim()) return messages;
    }
  }

  return typeof value.title === "string" && value.title.trim() ? value.title : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}