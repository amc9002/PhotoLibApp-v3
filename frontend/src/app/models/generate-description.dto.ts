export type DescriptionStyle = 'artistic' | 'informative' | 'scientific' | 'journalistic';

export type DescriptionLength = 'short' | 'medium' | 'long';

export interface GenerateDescriptionRequest {
  style: DescriptionStyle;
  length: DescriptionLength;
}

export interface GenerateDescriptionResponse {
  title: string;
  description: string;
  suggestedTags: string[];
}
