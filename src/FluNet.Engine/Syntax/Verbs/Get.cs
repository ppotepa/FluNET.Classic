using FluNET.Language;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs;

[Verb("GET")]
[Alias("FETCH")]
[Alias("RETRIEVE")]
public abstract class Get<TWhat, TFrom> : IVerb<TWhat, TFrom>, IGet, IWhat<TWhat>, IFrom<TFrom>
{
    protected Get(TWhat what, TFrom from)
    {
        What = what;
        From = from;
    }

    public TWhat What { get; protected set; }

    public TFrom From { get; protected set; }

    public string Text => "GET";

    public virtual string[] Synonyms => ["FETCH", "RETRIEVE"];

    public abstract Func<TFrom, TWhat> Act { get; }

    public abstract bool Validate(IWord word);

    public abstract TFrom? Resolve(string value);

    public virtual bool CanHandle(IWord root)
    {
        Keywords.From? fromPrep = root.Find<Keywords.From>();
        if (fromPrep?.Next is not IWord valueWord)
        {
            return false;
        }

        return Validate(valueWord);
    }

    public IWord? Next { get; set; }

    public IWord? Previous { get; set; }

    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        if (nextWord is Keywords.From)
        {
            return ValidationResult.Failure(
                "GET verb requires a subject (what to get). Expected [variable] or {reference} before FROM.");
        }

        if (nextWord is Words.QualifierWord)
        {
            return ValidationResult.Success();
        }

        bool isValidWhat = nextWord is Words.VariableWord
            || nextWord is Words.ReferenceWord
            || nextWord is IWhat<TWhat>;

        return isValidWhat
            ? ValidationResult.Success()
            : ValidationResult.Failure(
                "Invalid word after GET verb. Expected qualifier, [variable], or {reference} specifying what to get.");
    }

    public virtual TWhat Invoke() => Act(From);
}
