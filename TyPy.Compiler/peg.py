"""
grammar PEG;

compilation_unit: (statement? COMMENT? '\n')*;
statement: NAME ':' ('\n' '|')? rule ('\n' '|' rule)*;
rule: sequence COMMENT?;
sequence: alternative*;
alternative: sub_primary ('|' sub_primary)*;
optional: '[' rule ']' | primary '?';
many_or_none: primary '*';
many: primary '+';
separated_expr: primary '.' primary '+';
positive_lookahead: '&' primary;
negative_lookahead: '!' primary;
sub_primary: optional | many_or_none | many | separated_expr | positive_lookahead | negative_lookahead | primary;
cut: '~';
primary: '(' rule ')' | cut | NAME | STRING;


WHITESPACE: (' ' | '\t') -> skip;
COMMENT: '#' (~'\n')*;
NAME: [a-zA-Z_]+;
// probably a better way of doing this
STRING: '"' (~('"' | '\n')|'\\"')* '"' | '\'' (~('\'' | '\n')|'\\\'')* '\'';
"""
from enum import Enum
import string

translation_table = {}
visited = {}
rules = {}
cnt = 0
recursive_rules = []


class Lexemes(Enum):
    WHITESPACE = 1
    COMMENT = 2
    NAME = 3
    STRING = 4
    LPAREN = 5
    RPAREN = 6
    QMARK = 7
    EMARK = 8
    PIPE = 9
    PLUS = 10
    DOT = 11
    STAR = 12
    LBRAK = 13
    RBRAK = 14
    NEWLINE = 15
    COLON = 16
    CUT = 17
    AND = 18
    ENDMARKER = 19


def read_in_str(contents, start_index, valid_chars):
    cur_index = start_index
    while contents[cur_index] in valid_chars:
        cur_index += 1
    return cur_index


def lex(contents: str):
    contents += "\n"
    index = 0
    tokens = []
    while index < len(contents):
        if contents[index] == '\n':
            tokens.append((Lexemes.NEWLINE, '\n'))
            index += 1
        elif contents[index] == ':':
            tokens.append((Lexemes.COLON, ':'))
            index += 1
        elif contents[index] == '(':
            tokens.append((Lexemes.LPAREN, '('))
            index += 1
        elif contents[index] == ')':
            tokens.append((Lexemes.RPAREN, ')'))
            index += 1
        elif contents[index] == '?':
            tokens.append((Lexemes.QMARK, '?'))
            index += 1
        elif contents[index] == '!':
            tokens.append((Lexemes.EMARK, '!'))
            index += 1
        elif contents[index] == '|':
            tokens.append((Lexemes.PIPE, '|'))
            index += 1
        elif contents[index] == '+':
            tokens.append((Lexemes.PLUS, '+'))
            index += 1
        elif contents[index] == '.':
            tokens.append((Lexemes.DOT, '.'))
            index += 1
        elif contents[index] == '*':
            tokens.append((Lexemes.STAR, '*'))
            index += 1
        elif contents[index] == '[':
            tokens.append((Lexemes.LBRAK, '['))
            index += 1
        elif contents[index] == ']':
            tokens.append((Lexemes.RBRAK, ']'))
            index += 1
        elif contents[index] == '~':
            tokens.append((Lexemes.CUT, '~'))
            index += 1
        elif contents[index] == '&':
            tokens.append((Lexemes.AND, '&'))
            index += 1
        elif contents[index] == '#':
            end_index = contents.index("\n", index)
            tokens.append((Lexemes.COMMENT, contents[index:end_index]))
            index = end_index
        elif contents[index] == '\'':
            end_index = index + 1
            while True:
                if contents[end_index] == '\'' and contents[end_index-1] != '\\':
                    break
                if contents[end_index] == '\n':
                    raise Exception("Newline found in middle of a string literal.")
                end_index += 1
            tokens.append((Lexemes.STRING, contents[index:end_index+1]))
            index = end_index + 1
        elif contents[index] == ' ' or contents[index] == '\t':
            end_index = read_in_str(contents, index, ' \t')
            tokens.append((Lexemes.WHITESPACE, contents[index:end_index]))
            index = end_index
        else:
            end_index = read_in_str(contents, index, string.ascii_lowercase + string.ascii_uppercase + '_')
            if index == end_index:
                raise Exception(f"Invalid token: {contents[index]}")
            tokens.append((Lexemes.NAME, contents[index:end_index]))
            index = end_index
    return tokens


def peek(tokens, offset, index):
    if offset + index >= len(tokens):
        raise Exception("End of input")
    return tokens[offset + index]


def match(tokens, offset, token):
    if tokens[offset][0] == token:
        return tokens[offset]
    return None


def to_pascal(s):
    return s.lower().replace("_", " ").title().replace(" ", "")


class Statement:
    def __init__(self, name, rules):
        self.name = name
        self.rules = rules

    def compile(self):
        if len(self.rules) == 1:
            indent = '\t'
            return f"{{\n\tParseToken.{translation_table.get(self.name[1], to_pascal(self.name[1]))}, \n{self.rules[0].compile(indent)}\n}}"
        return "{\n\tParseToken." + translation_table.get(self.name[1], to_pascal(self.name[1])) + ",\n\tnew ParseAny(\n" + ',\n'.join(rule.compile("\t\t") for rule in self.rules) + "\n\t)\n}"


class Sequence:
    def __init__(self, seq):
        self.seq = seq

    def compile(self, indent=""):
        if len(self.seq) == 1:
            return self.seq[0].compile(indent)
        return f"{indent}new ParseSequence(\n" + ",\n".join(item.compile(indent + "\t") for item in self.seq) + f"\n{indent})"


class Alternative:
    def __init__(self, alt):
        self.alt = alt

    def compile(self, indent=""):
        if len(self.alt) == 1:
            return self.alt[0].compile(indent)
        return f"{indent}new ParseAny(\n" + ",\n".join(alternative.compile(indent + '\t') for alternative in self.alt) + f"\n{indent})"


class PositiveLookahead:
    def __init__(self, exp):
        self.exp = exp

    def compile(self, indent=""):
        return f"{indent}new LookaheadRequire({self.exp.compile()})"


class NegativeLookahead:
    def __init__(self, exp):
        self.exp = exp

    def compile(self, indent=""):
        return f"{indent}new LookaheadRequireNot({self.exp.compile()})"


class SeparatedExpression:
    def __init__(self, separator, exp):
        self.separator = separator
        self.exp = exp

    def compile(self, indent=""):
        return f"{indent}new ParseSeparated({self.separator.compile()}, {self.exp.compile()})"


class Many:
    def __init__(self, exp):
        self.exp = exp

    def compile(self, indent=""):
        return f"{indent}new ParseRepeat(\n" + self.exp.compile(indent+'\t') + f"\n{indent})"


class ManyOrNone:
    def __init__(self, exp):
        self.exp = exp

    def compile(self, indent=""):
        return f"{indent}new ParseOptional(new ParseRepeat(\n" + self.exp.compile(indent+'\t') + f"\n{indent}))"


class Optional:
    def __init__(self, exp):
        self.exp = exp

    def compile(self, indent=""):
        tab = '\t'
        return f"{indent}new ParseOptional(\n{self.exp.compile(indent+tab)}\n{indent})"


class Name:
    def __init__(self, lexeme):
        self.lexeme = lexeme

    def compile(self, indent=""):
        if self.lexeme[1].isupper():
            return f"{indent}new LexemeWrapper(LexToken.{translation_table.get(self.lexeme[1], to_pascal(self.lexeme[1]))})"
        return f"{indent}new TokenWrapper(ParseToken.{translation_table.get(self.lexeme[1], to_pascal(self.lexeme[1]))})"


class String:
    def __init__(self, lexeme):
        self.lexeme = lexeme

    def compile(self, indent=""):
        return indent + f"new LexemeWrapper(LexToken.{translation_table.get('str_' + self.lexeme[1][1:-1], self.lexeme[1])})"


class Cut:
    def __init__(self, lexeme):
        self.lexeme = lexeme

    def compile(self, indent=""):
        # we currently don't have a way to handle Cuts
        return f"{indent}new Cut()"


def compilation_unit(tokens, offset):
    success = True
    statements = []
    consumed_tokens_total = 0
    n = 0
    while success:
        stmt, consumed_tokens = statement(tokens, offset)
        if stmt is not None:
            n += 1
            statements.append(stmt)
            offset += consumed_tokens
            consumed_tokens_total += consumed_tokens_total
        comment = match(tokens, offset, Lexemes.COMMENT)
        if comment is not None:
            offset += 1
            consumed_tokens_total += 1
        nl = match(tokens, offset, Lexemes.NEWLINE)
        if nl is None:
            break
        offset += 1
        consumed_tokens_total += 1
    return statements


def statement(tokens, offset):
    initial_offset = offset
    name = match(tokens, offset, Lexemes.NAME)
    offset += 1
    if name is None:
        return None, 0

    colon = match(tokens, offset, Lexemes.COLON)
    offset += 1
    if colon is None:
        return None, 0

    nl = match(tokens, offset, Lexemes.NEWLINE)
    pipe = match(tokens, offset + 1, Lexemes.PIPE)
    if nl is not None and pipe is not None:
        offset += 2
    current_rule, off_change = rule(tokens, offset)
    rules = [current_rule]
    if current_rule is None:
        return None, 0
    offset += off_change

    while True:
        nl = match(tokens, offset, Lexemes.NEWLINE)
        pipe = match(tokens, offset + 1, Lexemes.PIPE)
        current_rule, off_change = rule(tokens, offset + 2)
        if nl is None or pipe is None or current_rule is None:
            break
        rules.append(current_rule)
        offset += off_change + 2
    return Statement(name, rules), offset - initial_offset


def rule(tokens, offset):
    initial_offset = offset
    alt, offset_change = alternative(tokens, offset)
    if alt is None:
        return None, 0
    offset += offset_change
    comment = match(tokens, offset, Lexemes.COMMENT)
    if comment is not None:
        offset += 1
    return alt, offset - initial_offset


def alternative(tokens, offset):
    initial_offset = offset
    subp, offset_change = sub_primary(tokens, offset)
    if subp is None:
        return None, 0
    offset += offset_change
    sequence = [subp]
    while subp is not None:
        subp, offset_change = sub_primary(tokens, offset)
        offset += offset_change
        if subp is not None:
            sequence.append(subp)
    alternatives = [Sequence(sequence)]
    while True:
        pipe = match(tokens, offset, Lexemes.PIPE)
        if pipe is None:
            break
        offset += 1
        subp, offset_change = sub_primary(tokens, offset)
        if subp is None:
            return None, 0
        offset += offset_change
        sequence = [subp]
        while subp is not None:
            subp, offset_change = sub_primary(tokens, offset)
            offset += offset_change
            if subp is not None:
                sequence.append(subp)
        alternatives.append(Sequence(sequence))
    return Alternative(alternatives), offset - initial_offset


def optional(tokens, offset):
    next = peek(tokens, offset, 0)[0]
    if next == Lexemes.LBRAK:
        prim, offset_change = rule(tokens, offset + 1)
        if prim is None:
            return None, 0
        Rbrak = match(tokens, offset + offset_change + 1, Lexemes.RBRAK)
        if Rbrak is None:
            return None, 0
        return Optional(prim), offset_change + 2
    prim, offset_change = primary(tokens, offset)
    if prim is None:
        return None, 0
    token = match(tokens, offset + offset_change, Lexemes.QMARK)
    if token is None:
        return None, 0
    return Optional(prim), offset_change + 1


def many_or_none(tokens, offset):
    prim, offset_change = primary(tokens, offset)
    if prim is None:
        return None, 0
    token = match(tokens, offset + offset_change, Lexemes.STAR)
    if token is None:
        return None, 0
    return ManyOrNone(prim), offset_change + 1


def many(tokens, offset):
    prim, offset_change = primary(tokens, offset)
    if prim is None:
        return None, 0
    token = match(tokens, offset + offset_change, Lexemes.PLUS)
    if token is None:
        return None, 0
    return Many(prim), offset_change + 1


def separated_expr(tokens, offset):
    prim1, offset_change = primary(tokens, offset)
    if prim1 is None:
        return None, 0
    dot = match(tokens, offset + offset_change, Lexemes.DOT)
    if dot is None:
        return None, 0
    prim2, offset_change_2 = primary(tokens, offset + offset_change + 1)
    if prim2 is None:
        return None, 0
    plus = match(tokens, offset + offset_change + offset_change_2 + 1, Lexemes.PLUS)
    if plus is None:
        return None, 0
    return SeparatedExpression(prim1, prim2), offset_change + offset_change_2 + 2


def positive_lookahead(tokens, offset):
    next = peek(tokens, offset, 0)[0]
    if next == Lexemes.AND:
        primary_exp, offset_change = primary(tokens, offset + 1)
        if primary is None:
            return None, 0
        return PositiveLookahead(primary_exp), offset_change + 1
    else:
        return None, 0


def negative_lookahead(tokens, offset):
    next = peek(tokens, offset, 0)[0]
    if next == Lexemes.EMARK:
        primary_exp, offset_change = primary(tokens, offset + 1)
        if primary is None:
            return None, 0
        return NegativeLookahead(primary_exp), offset_change + 1
    else:
        return None, 0


def sub_primary(tokens, offset):
    t_optional, offset_change = optional(tokens, offset)
    if t_optional is not None:
        return t_optional, offset_change

    t_many_or_none, offset_change = many_or_none(tokens, offset)
    if t_many_or_none is not None:
        return t_many_or_none, offset_change

    t_many, offset_change = many(tokens, offset)
    if t_many is not None:
        return t_many, offset_change

    t_separated_expr, offset_change = separated_expr(tokens, offset)
    if t_separated_expr is not None:
        return t_separated_expr, offset_change

    t_positive_lookahead, offset_change = positive_lookahead(tokens, offset)
    if t_positive_lookahead is not None:
        return t_positive_lookahead, offset_change

    t_negative_lookahead, offset_change = negative_lookahead(tokens, offset)
    if t_negative_lookahead is not None:
        return t_negative_lookahead, offset_change

    t_primary, offset_change = primary(tokens, offset)
    if t_primary is not None:
        return t_primary, offset_change

    return None, 0


def primary(tokens, offset):
    next, val = peek(tokens, offset, 0)
    if next == Lexemes.LPAREN:
        left_brac = match(tokens, offset, Lexemes.LPAREN)
        bracketed_rule, offset_change = rule(tokens, offset + 1)
        right_brac = match(tokens, offset + 1 + offset_change, Lexemes.RPAREN)
        if left_brac is None or bracketed_rule is None or right_brac is None:
            return None, 0
        return bracketed_rule, 2 + offset_change
    elif next == Lexemes.CUT:
        return Cut((next, val)), 1
    elif next == Lexemes.NAME:
        return Name((next, val)), 1
    elif next == Lexemes.STRING:
        return String((next, val)), 1
    else:
        return None, 0


def check_left_recursion(rule):
    """
    DFS the grammar to check for left-recursion
    """
    if rule in visited and visited[rule] == cnt:
        recursive_rules.append(rule)
        return True

    visited[rule] = cnt

    if isinstance(rule, Name) and not rule.lexeme[1].isupper():
        if rule.lexeme[1] not in rules:
            return False
        return check_left_recursion(rules[rule.lexeme[1]])
    if isinstance(rule, Statement):
        for sub_rule in rule.rules:
            check_left_recursion(sub_rule)
            if not (isinstance(sub_rule, Optional) or isinstance(sub_rule, ManyOrNone)):
                break

    if isinstance(rule, Sequence):
        for sub_rule in rule.seq:
            check_left_recursion(sub_rule)
            if not (isinstance(sub_rule, Optional) or isinstance(sub_rule, ManyOrNone)):
                break
    elif isinstance(rule, Alternative):
        for sub_rule in rule.alt:
            check_left_recursion(sub_rule)

    # Required terminal
    return False


if __name__ == '__main__':
    translation_file = open("Python9.5GrammarTokenLookup.csv", 'r')
    translation_table['str_,'] = 'Comma'
    for line in translation_file.read().split("\n"):
        *initial, translation = line.split(",")
        translation_table[",".join(initial)] = translation
    grammar_file = open("Python9.5Grammar.gram", 'r')
    lexed_tokens = lex(grammar_file.read())
    lexed_tokens.append((Lexemes.ENDMARKER, ''))
    grammar_file.close()
    lexed_tokens = list(filter(lambda x: x[0] != Lexemes.WHITESPACE, lexed_tokens))
    ast = compilation_unit(lexed_tokens, 0)
    rules = {x.name[1]: x for x in ast}
    for rule in ast:  # Check for left-recursion in O(n)
        if rule in visited:
            continue
        check_left_recursion(rule)
    print("Left-recursive rules: ")
    for rule in recursive_rules:
        print(rule.name[1])
    print("Unaltered compilation:")
    print(*list(map(lambda x: x.compile(), ast)), sep=",\n")
