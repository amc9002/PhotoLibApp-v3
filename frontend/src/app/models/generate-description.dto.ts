export type DescriptionStyle = 'artistic' | 'informative' | 'scientific' | 'journalistic';

export type DescriptionLength = 'short' | 'medium' | 'long';

export interface GenerateDescriptionRequest {
  style: DescriptionStyle;
  length: DescriptionLength;
  additionalInstructions?: string;
  /** UI language code (e.g. "be", "en") the generated text should be written in. */
  language?: string;
}

export interface GenerateDescriptionResponse {
  title: string;
  description: string;
  suggestedTags: string[];
}
