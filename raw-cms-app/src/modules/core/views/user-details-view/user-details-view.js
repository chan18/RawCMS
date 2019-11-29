import { optionalChain } from '../../../../utils/object.utils.js';
import { rawCmsDetailEditEvents } from '../../../shared/components/detail-edit/detail-edit.js';
import { UserDetailsDef } from '../../components/user-details/user-details.js';

const _UserDetailsView = async (res, rej) => {
  const tpl = await RawCMS.loadComponentTpl(
    '/modules/core/views/user-details-view/user-details-view.tpl.html'
  );
  const details = await UserDetailsDef();

  res({
    components: {
      UserDetails: details,
    },
    created: function() {
      RawCMS.eventBus.$on(rawCmsDetailEditEvents.loaded, ev => {
        this.updateTitle({
          isNewEntity: ev.isNewEntity,
          name: optionalChain(() => ev.value.UserName, { fallbackValue: '<NONE>' }),
        });
      });
    },
    data: function() {
      return {
        title: null,
      };
    },

    methods: {
      updateTitle: function({ isNewEntity, name }) {
        this.title = isNewEntity
          ? this.$t('core.users.detail.newTitle')
          : this.$t('core.users.detail.updateTitle', { name: name });
      },
    },
    template: tpl,
  });
};

export const UserDetailsView = _UserDetailsView;
export default _UserDetailsView;