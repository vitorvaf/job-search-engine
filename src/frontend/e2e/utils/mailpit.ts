type MailpitMessage = {
  ID?: string;
  Subject?: string;
};

type MailpitListResponse = {
  messages?: MailpitMessage[];
};

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export async function waitForTokenFromMailpit(params: {
  mailpitBaseUrl: string;
  recipientEmail: string;
  subjectIncludes: string;
  timeoutMs?: number;
  pollIntervalMs?: number;
}): Promise<string> {
  const {
    mailpitBaseUrl,
    recipientEmail,
    subjectIncludes,
    timeoutMs = 60_000,
    pollIntervalMs = 2_000,
  } = params;

  const start = Date.now();

  while (Date.now() - start < timeoutMs) {
    const listResponse = await fetch(`${mailpitBaseUrl.replace(/\/$/, "")}/api/v1/messages`, {
      cache: "no-store",
    });

    if (listResponse.ok) {
      const listPayload = (await listResponse.json()) as MailpitListResponse;
      const messages = Array.isArray(listPayload.messages) ? listPayload.messages : [];

      for (const message of messages) {
        const subject = message.Subject ?? "";
        if (!subject.toLowerCase().includes(subjectIncludes.toLowerCase())) {
          continue;
        }

        if (!message.ID) {
          continue;
        }

        const detailResponse = await fetch(
          `${mailpitBaseUrl.replace(/\/$/, "")}/api/v1/message/${encodeURIComponent(message.ID)}`,
          { cache: "no-store" },
        );

        if (!detailResponse.ok) {
          continue;
        }

        const detailPayload = await detailResponse.json();
        const detailRaw = JSON.stringify(detailPayload);

        if (!detailRaw.toLowerCase().includes(recipientEmail.toLowerCase())) {
          continue;
        }

        const tokenMatch = detailRaw.match(/token=([^\"&\s<]+)/i);
        if (tokenMatch?.[1]) {
          return decodeURIComponent(tokenMatch[1]);
        }
      }
    }

    await sleep(pollIntervalMs);
  }

  throw new Error(
    `Mailpit token not found for recipient '${recipientEmail}' and subject '${subjectIncludes}' within ${timeoutMs}ms.`,
  );
}
