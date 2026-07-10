export type Lang = 'be' | 'en';

/**
 * Flat key -> {be, en} dictionary. Keys are dotted by feature area so the
 * same "Cancel"/"Save"/"Close" wording isn't duplicated per modal.
 * Strings with a count use the `*Count` sibling functions in plurals.ts
 * instead of a plain entry here.
 */
export const translations: Record<string, Record<Lang, string>> = {
  // ---------------- common ----------------
  'common.cancel': { be: 'Скасаваць', en: 'Cancel' },
  'common.save': { be: 'Захаваць', en: 'Save' },
  'common.saving': { be: 'Захаванне…', en: 'Saving…' },
  'common.close': { be: 'Закрыць', en: 'Close' },
  'common.delete': { be: 'Выдаліць', en: 'Delete' },
  'common.create': { be: 'Стварыць', en: 'Create' },
  'common.working': { be: 'Апрацоўка…', en: 'Working…' },
  'common.confirm': { be: 'Пацвердзіць', en: 'Confirm' },
  'common.areYouSure': { be: 'Вы ўпэўнены?', en: 'Are you sure?' },

  // ---------------- app shell ----------------
  'app.selectGallery': { be: 'Абярыце галерэю, каб паглядзець фота', en: 'Select a gallery to view photos' },
  'app.addPhotosInput': { be: 'Дадаць фота ў галерэю', en: 'Add photos to gallery' },
  'app.deleteGalleryTitle': { be: 'Выдаліць галерэю?', en: 'Delete gallery?' },
  'app.deleteGalleryMessage': {
    be: 'Галерэя і ўсе яе фота будуць выдалены.',
    en: 'This gallery and all its photos will be removed.',
  },
  'app.emptyGalleryTitle': { be: 'Галерэя пустая', en: 'Gallery is empty' },
  'app.emptyGalleryMessage': {
    be: 'У гэтай галерэі больш няма фота. Выдаліць і яе таксама?',
    en: 'This gallery has no photos left. Delete it as well?',
  },
  'app.editGallery': { be: 'Рэдагаваць галерэю', en: 'Edit gallery' },
  'app.saveFailed': {
    be: 'Не ўдалося захаваць змены. Паспрабуйце яшчэ раз.',
    en: 'Failed to save changes. Please try again.',
  },

  // ---------------- toolbar ----------------
  'toolbar.gallery': { be: 'Галерэя ▼', en: 'Gallery ▼' },
  'toolbar.noGalleries': { be: 'Няма галерэй', en: 'No galleries' },
  'toolbar.gridView': { be: 'Рэжым пліткі', en: 'Grid view' },
  'toolbar.feedView': { be: 'Рэжым стужкі', en: 'Feed view' },
  'toolbar.increaseThumbSize': { be: 'Павялічыць мініяцюры', en: 'Increase thumbnail size' },
  'toolbar.decreaseThumbSize': { be: 'Паменшыць мініяцюры', en: 'Decrease thumbnail size' },
  'toolbar.copySelected': { be: 'Капіяваць вылучанае ў галерэю', en: 'Copy selected to gallery' },
  'toolbar.moveSelected': { be: 'Перамясціць вылучанае ў галерэю', en: 'Move selected to gallery' },
  'toolbar.deleteSelected': { be: 'Выдаліць вылучанае', en: 'Delete selected' },
  'toolbar.addPhotos': { be: 'Дадаць фота', en: 'Add photos' },
  'toolbar.addFromInternet': { be: 'Дадаць з інтэрнэту', en: 'Add from internet' },
  'toolbar.newGallery': { be: 'Новая галерэя', en: 'New gallery' },
  'toolbar.galleryProperties': { be: 'Уласцівасці галерэі', en: 'Gallery properties' },
  'toolbar.editGallery': { be: 'Рэдагаваць галерэю', en: 'Edit gallery' },
  'toolbar.deleteGallery': { be: 'Выдаліць галерэю', en: 'Delete gallery' },
  'toolbar.slideshow': { be: 'Слайд-шоу', en: 'Slideshow' },
  'toolbar.settings': { be: 'Налады', en: 'Settings' },

  // ---------------- create-gallery ----------------
  'createGallery.heading': { be: 'Новая галерэя', en: 'New gallery' },
  'createGallery.titlePlaceholder': { be: 'Назва галерэі', en: 'Gallery title' },
  'createGallery.confirmTitle': { be: 'Стварыць галерэю?', en: 'Create gallery?' },
  'createGallery.confirmMessagePrefix': { be: 'Стварыць новую галерэю "', en: 'Create a new gallery "' },
  'createGallery.confirmMessageSuffix': { be: '"?', en: '"?' },

  // ---------------- gallery-page ----------------
  'galleryPage.deletePhotoTitle': { be: 'Выдаліць фота?', en: 'Delete photo?' },

  // ---------------- gallery-properties ----------------
  'galleryProperties.heading': { be: 'Уласцівасці галерэі', en: 'Gallery properties' },
  'galleryProperties.title': { be: 'Назва:', en: 'Title:' },
  'galleryProperties.description': { be: 'Апісанне:', en: 'Description:' },
  'galleryProperties.tags': { be: 'Тэгі:', en: 'Tags:' },
  'galleryProperties.id': { be: 'ID:', en: 'ID:' },
  'galleryProperties.created': { be: 'Створана:', en: 'Created:' },
  'galleryProperties.updated': { be: 'Абноўлена:', en: 'Updated:' },

  // ---------------- gallery-view ----------------
  'galleryView.emptyState': {
    be: 'У гэтай галерэі пакуль няма фота. Дадайце першае праз меню "⋮".',
    en: 'This gallery has no photos yet. Add the first one from the "⋮" menu.',
  },

  // ---------------- gallery-select-modal ----------------
  'gallerySelect.noOtherGalleries': { be: 'Іншых галерэй пакуль няма', en: 'No other galleries yet' },
  'gallerySelect.newGalleryPlaceholder': { be: 'Ці стварыце новую галерэю…', en: 'Or create a new gallery…' },
  'gallerySelect.copy': { be: 'Капіяваць', en: 'Copy' },
  'gallerySelect.move': { be: 'Перамясціць', en: 'Move' },

  // ---------------- photo-actions ----------------
  'photoActions.menuLabel': { be: 'Дзеянні з фота', en: 'Photo actions' },
  'photoActions.editInfo': { be: 'Рэдагаваць звесткі', en: 'Edit info' },
  'photoActions.photoInfo': { be: 'Інфармацыя пра фота', en: 'Photo info' },
  'photoActions.copyToGallery': { be: 'Капіяваць у галерэю', en: 'Copy to gallery' },
  'photoActions.moveToGallery': { be: 'Перамясціць у галерэю', en: 'Move to gallery' },
  'photoActions.deletePhoto': { be: 'Выдаліць фота', en: 'Delete photo' },

  // ---------------- photo-info-modal ----------------
  'photoInfo.heading': { be: 'Інфармацыя пра фота', en: 'Photo info' },
  'photoInfo.camera': { be: 'Камера', en: 'Camera' },
  'photoInfo.dateTaken': { be: 'Дата здымку', en: 'Date taken' },
  'photoInfo.exposure': { be: 'Экспазіцыя', en: 'Exposure' },
  'photoInfo.noExif': { be: 'Для гэтага фота няма EXIF-звестак.', en: 'No EXIF metadata found for this photo.' },
  'photoInfo.noLocation': { be: 'Няма геаданых.', en: 'No location data.' },
  'photoInfo.hideFullExif': { be: 'Схаваць увесь EXIF', en: 'Hide full EXIF' },
  'photoInfo.showFullExif': { be: 'Паказаць увесь EXIF', en: 'Show full EXIF' },

  // ---------------- photo-viewer ----------------
  'photoViewer.previousPhoto': { be: 'Папярэдняе фота', en: 'Previous photo' },
  'photoViewer.nextPhoto': { be: 'Наступнае фота', en: 'Next photo' },
  'photoViewer.editDetails': { be: 'Рэдагаваць звесткі', en: 'Edit details' },
  'photoViewer.saveFailed': {
    be: 'Не ўдалося захаваць змены. Паспрабуйце яшчэ раз.',
    en: 'Failed to save changes. Please try again.',
  },
  'photoViewer.untitled': { be: 'Без назвы', en: 'Untitled' },
  'photoViewer.noDescription': { be: 'Без апісання', en: 'No description' },
  'photoViewer.collapseInfo': { be: 'Схаваць звесткі пра фота', en: 'Hide photo info' },
  'photoViewer.expandInfo': { be: 'Паказаць звесткі пра фота', en: 'Show photo info' },
  'photoViewer.stopSlideshow': { be: 'Спыніць слайд-шоу', en: 'Stop slideshow' },
  'photoViewer.enterFullscreen': { be: 'На ўвесь экран', en: 'Enter fullscreen' },
  'photoViewer.exitFullscreen': { be: 'Выйсці з поўнага экрана', en: 'Exit fullscreen' },

  // ---------------- slideshow-settings-modal ----------------
  'slideshow.heading': { be: 'Слайд-шоу', en: 'Slideshow' },
  'slideshow.order': { be: 'Парадак', en: 'Order' },
  'slideshow.orderForward': { be: 'У парадку', en: 'In order' },
  'slideshow.orderReverse': { be: 'У адваротным парадку', en: 'Reverse order' },
  'slideshow.orderRandom': { be: 'Выпадкова', en: 'Random' },
  'slideshow.interval': { be: 'Інтэрвал (сек.)', en: 'Interval (sec.)' },
  'slideshow.transitionDuration': { be: 'Працягласць пераходу (сек.)', en: 'Transition duration (sec.)' },
  'slideshow.start': { be: 'Пачаць', en: 'Start' },

  // ---------------- add-from-internet-modal ----------------
  'addFromInternet.heading': { be: 'Дадаць з інтэрнэту', en: 'Add from internet' },
  'addFromInternet.instructions': {
    be: 'Перацягні гэтую спасылку ў панэль закладак браўзера. На любой старонцы ў інтэрнэце націсні закладку, потым клікні патрэбнае фота — яно дадасца ў галерэю',
    en: 'Drag this link to your browser\'s bookmarks bar. On any page online, click the bookmark, then click the photo you want - it will be added to gallery',
  },
  'addFromInternet.bookmarkletPrefix': { be: '📷 Дадаць у «', en: '📷 Add to «' },
  'addFromInternet.bookmarkletSuffix': { be: '»', en: '»' },
  'addFromInternet.hint1': {
    be: 'Пасля кліку па фота коратка адкрыецца маленькае акенца — яно само зачыніцца, калі фота дадасца. Калі браўзер заблакіраваў яго як усплывальнае акно, дазволь папапы для гэтага сайта.',
    en: 'After clicking a photo, a small window briefly opens - it closes itself once the photo is added. If your browser blocked it as a popup, allow popups for this site.',
  },
  'addFromInternet.hint2': {
    be: 'Закладка прывязана да гэтай галерэі. Каб дадаваць фота ў іншую галерэю, адкрый гэтае акно зноў, калі яна выбрана, і перацягні новую спасылку.',
    en: 'The bookmark is tied to this gallery. To add photos to a different gallery, open this window again while it\'s selected, and drag a new link.',
  },

  // ---------------- edit-metadata-modal ----------------
  'editMetadata.heading': { be: 'Рэдагаваць звесткі', en: 'Edit details' },
  'editMetadata.title': { be: 'Назва', en: 'Title' },
  'editMetadata.description': { be: 'Апісанне', en: 'Description' },
  'editMetadata.tags': { be: 'Тэгі', en: 'Tags' },
  'editMetadata.tagsPlaceholder': { be: 'напр. караблі, вінтаж', en: 'e.g. ships, vintage' },
  'editMetadata.aiHeading': { be: 'Стварыць з дапамогай ШІ', en: 'Generate with AI' },
  'editMetadata.aiStyle': { be: 'Стыль', en: 'Style' },
  'editMetadata.aiStyleArtistic': { be: 'Мастацкі', en: 'Artistic' },
  'editMetadata.aiStyleInformative': { be: 'Пазнавальны', en: 'Informative' },
  'editMetadata.aiStyleScientific': { be: 'Навуковы', en: 'Scientific' },
  'editMetadata.aiStyleJournalistic': { be: 'Публіцыстычны', en: 'Journalistic' },
  'editMetadata.aiLength': { be: 'Аб’ём', en: 'Length' },
  'editMetadata.aiLengthShort': { be: 'Кароткі', en: 'Short' },
  'editMetadata.aiLengthMedium': { be: 'Сярэдні', en: 'Medium' },
  'editMetadata.aiLengthLong': { be: 'Падрабязны', en: 'Long' },
  'editMetadata.aiInstructionsPlaceholder': {
    be: 'Дадатковыя інструкцыі (мова, вядомыя факты і г.д.)',
    en: 'Additional instructions (language, known facts, etc.)',
  },
  'editMetadata.aiGenerate': { be: 'Стварыць', en: 'Generate' },
  'editMetadata.aiGenerating': { be: 'Стварэнне…', en: 'Generating…' },
  'editMetadata.aiFailed': {
    be: 'Не ўдалося стварыць апісанне. Паспрабуйце яшчэ раз.',
    en: 'Failed to generate a description. Please try again.',
  },
  'editMetadata.aiRateLimited': {
    be: 'Занадта шмат запытаў да ШІ за апошні час. Паспрабуйце пазней.',
    en: 'Too many AI requests recently. Please try again later.',
  },

  // ---------------- sync-review-modal ----------------
  'syncReview.heading': { be: 'Сінхранізацыя змен', en: 'Sync changes' },
  'syncReview.intro': {
    be: 'Пакуль не было сувязі з серверам, гэтыя змены захаваліся лакальна. Пацвердзі іх па адной ці ўсе разам.',
    en: 'While the server was unreachable, these changes were saved locally. Confirm them one by one or all at once.',
  },
  'syncReview.conflictsHint': {
    be: 'Гэтыя аб\'екты зменены на серверы, пакуль не было сувязі.',
    en: 'These items changed on the server while you were offline.',
  },
  'syncReview.conflictsHeading': { be: 'Канфлікты', en: 'Conflicts' },
  'syncReview.addedHeading': { be: 'Дададзена', en: 'Added' },
  'syncReview.deletedHeading': { be: 'Выдалена', en: 'Deleted' },
  'syncReview.changedHeading': { be: 'Зменена', en: 'Changed' },
  'syncReview.discard': { be: 'Прапусціць', en: 'Discard' },
  'syncReview.applyAnyway': { be: 'Усё роўна ўжыць', en: 'Apply anyway' },
  'syncReview.restore': { be: 'Вярнуць', en: 'Restore' },
  'syncReview.discardAll': { be: 'Скасаваць усе', en: 'Discard all' },
  'syncReview.confirmAll': { be: 'Пацвердзіць усе', en: 'Confirm all' },

  // ---------------- settings modal ----------------
  'settings.heading': { be: 'Налады', en: 'Settings' },
  'settings.language': { be: 'Мова', en: 'Language' },
  'settings.languageBe': { be: 'Беларуская', en: 'Belarusian' },
  'settings.languageEn': { be: 'Англійская', en: 'English' },
  'settings.theme': { be: 'Фон прыкладання', en: 'App background' },
  'settings.themeNavy': { be: 'Наві', en: 'Navy' },
  'settings.themeCharcoal': { be: 'Вугальны', en: 'Charcoal' },
  'settings.themeBlack': { be: 'Чысты чорны', en: 'True black' },
};
