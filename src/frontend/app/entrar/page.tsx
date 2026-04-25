import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { SignInForm } from "@/components/auth/sign-in-form";

export default async function EntrarPage() {
  const session = await auth();

  if (session?.user?.id) {
    redirect("/");
  }

  return <SignInForm />;
}
