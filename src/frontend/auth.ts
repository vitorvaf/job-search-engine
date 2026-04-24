import { getServerSession, type DefaultSession, type NextAuthOptions } from "next-auth";
import CredentialsProvider from "next-auth/providers/credentials";
import GitHubProvider from "next-auth/providers/github";
import GoogleProvider from "next-auth/providers/google";
import { getBackendUrl, getInternalApiHeaders } from "@/lib/api-proxy";

type BackendAuthUser = {
  userId: string;
  email?: string | null;
  displayName?: string | null;
  avatarUrl?: string | null;
};

type GithubEmailRecord = {
  email?: string;
  verified?: boolean;
  primary?: boolean;
};

function readStringValue(record: unknown, key: string): string | null {
  if (!record || typeof record !== "object") {
    return null;
  }

  const value = (record as Record<string, unknown>)[key];
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function readBooleanValue(record: unknown, key: string): boolean {
  if (!record || typeof record !== "object") {
    return false;
  }

  return Boolean((record as Record<string, unknown>)[key]);
}

function toAuthUser(payload: BackendAuthUser): DefaultSession["user"] & { id: string } {
  return {
    id: payload.userId,
    email: payload.email ?? null,
    name: payload.displayName ?? null,
    image: payload.avatarUrl ?? null,
  };
}

async function fetchGithubVerifiedEmail(accessToken: string): Promise<string | null> {
  const response = await fetch("https://api.github.com/user/emails", {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      Accept: "application/vnd.github+json",
      "User-Agent": "jobs-web-auth",
    },
    cache: "no-store",
  });

  if (!response.ok) {
    return null;
  }

  let payload: unknown;

  try {
    payload = (await response.json()) as unknown;
  } catch {
    return null;
  }

  if (!Array.isArray(payload)) {
    return null;
  }

  const records = payload as GithubEmailRecord[];
  const primaryVerified = records.find((item) => item.primary && item.verified && typeof item.email === "string");
  if (primaryVerified?.email) {
    return primaryVerified.email;
  }

  const anyVerified = records.find((item) => item.verified && typeof item.email === "string");
  return anyVerified?.email ?? null;
}

export const authOptions: NextAuthOptions = {
  session: { strategy: "jwt" },
  pages: { signIn: "/entrar" },
  providers: [
    CredentialsProvider({
      name: "Credentials",
      credentials: {
        email: { label: "Email", type: "email" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        const email = typeof credentials?.email === "string" ? credentials.email.trim() : "";
        const password = typeof credentials?.password === "string" ? credentials.password : "";

        if (!email || !password) {
          return null;
        }

        const response = await fetch(`${getBackendUrl()}/api/account/auth/credentials`, {
          method: "POST",
          headers: getInternalApiHeaders(),
          body: JSON.stringify({ email, password }),
          cache: "no-store",
        });

        if (!response.ok) {
          return null;
        }

        const payload = (await response.json()) as BackendAuthUser;
        if (!payload?.userId) {
          return null;
        }

        return toAuthUser(payload);
      },
    }),
    GitHubProvider({
      clientId: process.env.AUTH_GITHUB_ID ?? "",
      clientSecret: process.env.AUTH_GITHUB_SECRET ?? "",
      authorization: { params: { scope: "read:user user:email" } },
    }),
    GoogleProvider({
      clientId: process.env.AUTH_GOOGLE_ID ?? "",
      clientSecret: process.env.AUTH_GOOGLE_SECRET ?? "",
    }),
  ],
  callbacks: {
    async signIn({ user, account, profile }) {
      if (!account) {
        return false;
      }

      if (account.provider === "credentials") {
        return Boolean(user.id);
      }

      const providerUserId =
        readStringValue(profile, "sub") ??
        readStringValue(profile, "id") ??
        (typeof account.providerAccountId === "string" ? account.providerAccountId : null);

      if (!providerUserId) {
        return false;
      }

      let email = (typeof user.email === "string" && user.email.trim() ? user.email.trim() : null) ?? readStringValue(profile, "email");
      let isEmailVerified = false;

      if (account.provider === "google") {
        isEmailVerified = readBooleanValue(profile, "email_verified");
      }

      if (account.provider === "github") {
        const accessToken = typeof account.access_token === "string" ? account.access_token : "";
        if (!accessToken) {
          return false;
        }

        const verifiedEmail = await fetchGithubVerifiedEmail(accessToken);
        if (!verifiedEmail) {
          return false;
        }

        email = verifiedEmail;
        isEmailVerified = true;
      }

      if (!email || !isEmailVerified) {
        return false;
      }

      const response = await fetch(`${getBackendUrl()}/api/account/auth/oauth`, {
        method: "POST",
        headers: getInternalApiHeaders(),
        body: JSON.stringify({
          provider: account.provider,
          providerUserId,
          email,
          isEmailVerified,
          displayName: user.name,
          avatarUrl: user.image,
        }),
        cache: "no-store",
      });

      if (!response.ok) {
        return false;
      }

      const payload = (await response.json()) as BackendAuthUser;
      if (!payload?.userId) {
        return false;
      }

      user.id = payload.userId;
      user.email = payload.email ?? user.email;
      user.name = payload.displayName ?? user.name;
      user.image = payload.avatarUrl ?? user.image;

      return true;
    },
    async jwt({ token, user }) {
      const userIdFromSignIn = typeof user?.id === "string" ? user.id.trim() : "";

      if (userIdFromSignIn) {
        token.userId = userIdFromSignIn;
        token.sub = userIdFromSignIn;
      }

      if (!token.userId && typeof token.sub === "string" && token.sub.trim()) {
        token.userId = token.sub.trim();
      }

      return token;
    },
    async session({ session, token }) {
      const resolvedUserId =
        (typeof token.userId === "string" && token.userId.trim() ? token.userId.trim() : null) ??
        (typeof token.sub === "string" && token.sub.trim() ? token.sub.trim() : null) ??
        "";

      if (!session.user) {
        session.user = {
          id: resolvedUserId,
          name: null,
          email: null,
          image: null,
        };
      } else {
        session.user.id = resolvedUserId;
      }

      return session;
    },
  },
};

export function auth() {
  return getServerSession(authOptions);
}
