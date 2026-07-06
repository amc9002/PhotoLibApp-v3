export interface Gallery {
  id: string;
  title: string;
  description?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  isDeleted: boolean;
  tags: string[];
}
