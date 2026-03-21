import { Suspense } from "react";
import { JobsListPage } from "@/components/jobs-list-page";
import { SkeletonList } from "@/components/skeleton-list";

export default function HomePage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <JobsListPage />
    </Suspense>
  );
}
