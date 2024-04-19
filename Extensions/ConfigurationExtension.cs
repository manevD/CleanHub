namespace CleanHub.Extensions
{
    public static class ConfigurationExtension
    {
        public static T AddConfiguration<T> (this IConfigurationRoot configuration,IServiceCollection services, string configSectionPath)
        where T: class , new()
        {
            var section = configuration.GetSection (configSectionPath);
            var config = section?.Get<T>()?? new T ();
            services.AddOptions<T>().Bind(section!).ValidateDataAnnotations().ValidateOnStart();
            return config;
        }
    }
}
