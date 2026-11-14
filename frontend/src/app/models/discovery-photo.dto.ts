import { PhotoListItemDto } from './photoLisrItem.dto';

/** A Discovery feed item - a regular photo plus the owner of its gallery, shown on hover instead of tags. */
export interface DiscoveryPhotoDto extends PhotoListItemDto {
  ownerName: string;
}
